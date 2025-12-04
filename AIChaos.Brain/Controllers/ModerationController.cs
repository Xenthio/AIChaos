using AIChaos.Brain.Models;
using AIChaos.Brain.Services;
using Microsoft.AspNetCore.Mvc;

namespace AIChaos.Brain.Controllers;

/// <summary>
/// Controller for image moderation and refund endpoints.
/// Requires moderation password authentication via X-Moderation-Password header.
/// </summary>
[ApiController]
[Route("api/moderation")]
public class ModerationController : ControllerBase
{
    private readonly ImageModerationService _moderationService;
    private readonly CommandQueueService _commandQueue;
    private readonly AiCodeGeneratorService _codeGenerator;
    private readonly SettingsService _settingsService;
    private readonly RefundService _refundService;
    private readonly TestClientService _testClientService;
    private readonly ILogger<ModerationController> _logger;
    
    // Reasons that trigger a real refund request (others show fake "Submitted" success)
    private static readonly string[] RealRefundReasons = new[]
    {
        "My request didn't work",
        "The streamer didn't see my request"
    };
    
    public ModerationController(
        ImageModerationService moderationService,
        CommandQueueService commandQueue,
        AiCodeGeneratorService codeGenerator,
        SettingsService settingsService,
        RefundService refundService,
        TestClientService testClientService,
        ILogger<ModerationController> logger)
    {
        _moderationService = moderationService;
        _commandQueue = commandQueue;
        _codeGenerator = codeGenerator;
        _settingsService = settingsService;
        _refundService = refundService;
        _testClientService = testClientService;
        _logger = logger;
    }
    
    /// <summary>
    /// Validates the moderation password from request header.
    /// </summary>
    private bool IsAuthenticated()
    {
        var password = Request.Headers["X-Moderation-Password"].FirstOrDefault();
        return !string.IsNullOrEmpty(password) && 
               _settingsService.ValidateModerationPassword(password);
    }
    
    /// <summary>
    /// Returns unauthorized response for missing/invalid moderation password.
    /// </summary>
    private ActionResult UnauthorizedModerationAccess()
    {
        return StatusCode(401, new ApiResponse
        {
            Status = "error",
            Message = "Moderation password required. Include X-Moderation-Password header."
        });
    }
    
    /// <summary>
    /// Gets all pending images awaiting moderation.
    /// </summary>
    [HttpGet("pending")]
    public ActionResult<PendingImagesResponse> GetPendingImages()
    {
        if (!IsAuthenticated()) return UnauthorizedModerationAccess();
        
        var images = _moderationService.GetPendingImages();
        return Ok(new PendingImagesResponse
        {
            Images = images,
            TotalPending = images.Count
        });
    }
    
    /// <summary>
    /// Gets all images (including reviewed).
    /// </summary>
    [HttpGet("all")]
    public ActionResult<PendingImagesResponse> GetAllImages()
    {
        if (!IsAuthenticated()) return UnauthorizedModerationAccess();
        
        var images = _moderationService.GetAllImages();
        return Ok(new PendingImagesResponse
        {
            Images = images,
            TotalPending = _moderationService.PendingCount
        });
    }
    
    /// <summary>
    /// Approves an image and processes the associated prompt.
    /// </summary>
    [HttpPost("approve")]
    public async Task<ActionResult<ApiResponse>> ApproveImage([FromBody] ImageReviewRequest request)
    {
        if (!IsAuthenticated()) return UnauthorizedModerationAccess();
        
        var entry = _moderationService.ApproveImage(request.ImageId);
        if (entry == null)
        {
            return NotFound(new ApiResponse
            {
                Status = "error",
                Message = "Image not found"
            });
        }
        
        _logger.LogInformation("[MODERATION] Processing approved image #{ImageId} for: {Prompt}", 
            request.ImageId, entry.UserPrompt);
        
        // Check if there's an existing placeholder command for this image
        CommandEntry? command = null;
        
        if (entry.CommandId.HasValue)
        {
            // Get the existing command that was created when the user submitted
            command = _commandQueue.GetCommand(entry.CommandId.Value);
            
            if (command != null && command.Status == CommandStatus.PendingModeration)
            {
                _logger.LogInformation("[MODERATION] Found existing command #{CommandId} in PendingModeration status", 
                    command.Id);
                
                // Check if this was an interactive mode submission
                var isInteractiveMode = command.AiResponse?.Contains("[Interactive Mode]") ?? false;
                
                if (isInteractiveMode)
                {
                    // For interactive mode, we need to trigger an interactive session instead of just generating code
                    _logger.LogInformation("[MODERATION] Approved image is for interactive mode - starting interactive session");
                    
                    // Update command status to show it's being processed
                    command.Status = CommandStatus.Queued;
                    command.AiResponse = "?? Image approved - starting interactive session...";
                    command.ImageContext = entry.ImageUrl;
                    
                    // We can't directly start an interactive session from here because we need the full
                    // AccountService flow. Instead, we'll mark it for processing and let the client
                    // poll for updates or trigger the session manually.
                    
                    // TODO: Consider adding a callback mechanism or webhook to notify the client
                    // For now, the command will be in Queued status with a message indicating
                    // that the moderator should manually trigger the interactive session
                    
                    return Ok(new ApiResponse
                    {
                        Status = "success",
                        Message = "Image approved for interactive mode. User should refresh or resubmit the prompt.",
                        CommandId = command.Id
                    });
                }
                
                // Regular mode (non-interactive) - generate code and queue
                _logger.LogInformation("[MODERATION] Generating code for regular (non-interactive) mode");
                
                // Generate code with image context now that it's approved
                var (executionCode, undoCode) = await _codeGenerator.GenerateCodeAsync(
                    entry.UserPrompt,
                    imageContext: $"Image URL: {entry.ImageUrl}");
                
                _logger.LogInformation("[MODERATION] Code generated successfully. Execution code length: {Length}", 
                    executionCode.Length);
                
                // Update the existing command
                command.ExecutionCode = executionCode;
                command.UndoCode = undoCode;
                command.ImageContext = entry.ImageUrl;
                command.AiResponse = null; // Clear the "waiting for moderation" message
                command.Status = CommandStatus.Queued;
                
                // Check if test client mode is enabled
                var isTestClientModeEnabled = _settingsService.Settings.TestClient.Enabled;
                
                _logger.LogInformation("[MODERATION] Test client mode: {Enabled}. Queueing command #{CommandId}", 
                    isTestClientModeEnabled ? "ENABLED" : "DISABLED", command.Id);
                
                // Queue for execution (respect test client mode)
                if (isTestClientModeEnabled)
                {
                    _testClientService.QueueForTesting(command.Id, executionCode, entry.UserPrompt);
                    _logger.LogInformation("[MODERATION] ? Command #{CommandId} queued for testing on test client", command.Id);
                }
                else
                {
                    // Add to execution queue
                    _commandQueue.QueueCommand(command);
                    var queueCount = _commandQueue.GetQueueCount();
                    _logger.LogInformation("[MODERATION] ? Command #{CommandId} queued for execution. Queue depth: {QueueCount}", 
                        command.Id, queueCount);
                }
                
                return Ok(new ApiResponse
                {
                    Status = "success",
                    Message = isTestClientModeEnabled 
                        ? "Image approved and command queued for testing" 
                        : "Image approved and command queued for execution",
                    CommandId = command.Id
                });
            }
            else if (command != null)
            {
                _logger.LogWarning("[MODERATION] Command #{CommandId} exists but has status {Status} (expected PendingModeration)", 
                    command.Id, command.Status);
            }
            else
            {
                _logger.LogWarning("[MODERATION] No command found with ID {CommandId}", entry.CommandId.Value);
            }
        }
        else
        {
            _logger.LogWarning("[MODERATION] PendingImageEntry #{ImageId} has no associated CommandId", request.ImageId);
        }
        
        // Fallback: If no existing command was found (shouldn't happen), create a new one
        _logger.LogWarning("[MODERATION] FALLBACK: Creating new command for image #{ImageId}", request.ImageId);
        
        // Generate code with image context now that it's approved
        var (execCode, undoCodeNew) = await _codeGenerator.GenerateCodeAsync(
            entry.UserPrompt,
            imageContext: $"Image URL: {entry.ImageUrl}");
        
        // Check if test client mode is enabled
        var testClientEnabled = _settingsService.Settings.TestClient.Enabled;
        
        // Add to command queue (respect test client mode)
        command = _commandQueue.AddCommand(
            entry.UserPrompt,
            execCode,
            undoCodeNew,
            entry.Source,
            entry.Author,
            entry.ImageUrl,
            entry.UserId,
            null,
            queueForExecution: !testClientEnabled);
        
        // If test client mode is enabled, queue for testing
        if (testClientEnabled)
        {
            _testClientService.QueueForTesting(command.Id, execCode, entry.UserPrompt);
            _logger.LogInformation("[MODERATION] Approved image command #{CommandId} queued for testing", command.Id);
        }
        else
        {
            _logger.LogInformation("[MODERATION] Approved image command #{CommandId} queued for execution", command.Id);
        }
        
        return Ok(new ApiResponse
        {
            Status = "success",
            Message = "Image approved and command queued",
            CommandId = command.Id
        });
    }
    
    /// <summary>
    /// Denies an image.
    /// </summary>
    [HttpPost("deny")]
    public ActionResult<ApiResponse> DenyImage([FromBody] ImageReviewRequest request)
    {
        if (!IsAuthenticated()) return UnauthorizedModerationAccess();
        
        var entry = _moderationService.DenyImage(request.ImageId);
        if (entry == null)
        {
            return NotFound(new ApiResponse
            {
                Status = "error",
                Message = "Image not found"
            });
        }
        
        return Ok(new ApiResponse
        {
            Status = "success",
            Message = "Image denied"
        });
    }
    
    /// <summary>
    /// Gets the count of pending images.
    /// </summary>
    [HttpGet("count")]
    public ActionResult GetPendingCount()
    {
        if (!IsAuthenticated()) return UnauthorizedModerationAccess();
        
        return Ok(new { count = _moderationService.PendingCount });
    }
    
    // ==========================================
    // REFUND ENDPOINTS
    // ==========================================
    
    /// <summary>
    /// Submits a refund request. Public endpoint (no auth required).
    /// For "fake" reasons, just returns success without creating a real request.
    /// </summary>
    [HttpPost("refund/request")]
    public ActionResult RequestRefund([FromBody] RefundRequestPayload request)
    {
        if (string.IsNullOrEmpty(request.UserId))
        {
            return BadRequest(new { status = "error", message = "User ID required" });
        }
        
        if (string.IsNullOrEmpty(request.Reason))
        {
            return BadRequest(new { status = "error", message = "Reason required" });
        }
        
        // Check if this is a "real" reason that should trigger moderation review
        var isRealReason = RealRefundReasons.Any(r => 
            r.Equals(request.Reason, StringComparison.OrdinalIgnoreCase));
        
        if (!isRealReason)
        {
            // Fake submission - log it but don't create a real request
            _logger.LogInformation("[REFUND] Fake refund request from {User}: {Reason} (ignored)", 
                request.UserDisplayName, request.Reason);
            
            return Ok(new { 
                status = "success", 
                message = "Your report has been submitted. Thank you for your feedback!" 
            });
        }
        
        // Get the command to find the prompt for audit purposes
        var command = _commandQueue.GetCommand(request.CommandId);
        if (command == null)
        {
            return NotFound(new { 
                status = "error", 
                message = "Command not found. Cannot process refund request." 
            });
        }
        
        // Create real refund request
        var refundRequest = _refundService.CreateRequest(
            request.UserId,
            request.UserDisplayName ?? "Unknown",
            request.CommandId,
            command.UserPrompt,
            request.Reason,
            Constants.CommandCost
        );
        
        return Ok(new { 
            status = "success", 
            message = "Your refund request has been submitted for review.",
            requestId = refundRequest.Id
        });
    }
    
    /// <summary>
    /// Gets all pending refund requests. Requires moderation auth.
    /// </summary>
    [HttpGet("refund/pending")]
    public ActionResult GetPendingRefunds()
    {
        if (!IsAuthenticated()) return UnauthorizedModerationAccess();
        
        var requests = _refundService.GetPendingRequests();
        return Ok(requests);
    }
    
    /// <summary>
    /// Approves a refund request and returns credits to user. Requires moderation auth.
    /// </summary>
    [HttpPost("refund/approve")]
    public ActionResult ApproveRefund([FromBody] RefundActionPayload request)
    {
        if (!IsAuthenticated()) return UnauthorizedModerationAccess();
        
        if (string.IsNullOrEmpty(request.RequestId))
        {
            return BadRequest(new { status = "error", message = "Request ID required" });
        }
        
        if (_refundService.ApproveRefund(request.RequestId))
        {
            return Ok(new { status = "success", message = "Refund approved and credits returned" });
        }
        
        return NotFound(new { status = "error", message = "Refund request not found or already processed" });
    }
    
    /// <summary>
    /// Rejects a refund request. Requires moderation auth.
    /// </summary>
    [HttpPost("refund/reject")]
    public ActionResult RejectRefund([FromBody] RefundActionPayload request)
    {
        if (!IsAuthenticated()) return UnauthorizedModerationAccess();
        
        if (string.IsNullOrEmpty(request.RequestId))
        {
            return BadRequest(new { status = "error", message = "Request ID required" });
        }
        
        if (_refundService.RejectRefund(request.RequestId))
        {
            return Ok(new { status = "success", message = "Refund request rejected" });
        }
        
        return NotFound(new { status = "error", message = "Refund request not found or already processed" });
    }
}

/// <summary>
/// Payload for submitting a refund request.
/// </summary>
public class RefundRequestPayload
{
    public string UserId { get; set; } = "";
    public string? UserDisplayName { get; set; }
    public int CommandId { get; set; }
    public string Reason { get; set; } = "";
}

/// <summary>
/// Payload for approving or rejecting a refund.
/// </summary>
public class RefundActionPayload
{
    public string RequestId { get; set; } = "";
}
