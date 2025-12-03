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
        
        _logger.LogInformation("[MODERATION] Processing approved image for: {Prompt}", entry.UserPrompt);
        
        // Generate code with image context now that it's approved
        var (executionCode, undoCode) = await _codeGenerator.GenerateCodeAsync(
            entry.UserPrompt,
            imageContext: $"Image URL: {entry.ImageUrl}");
        
        // Check if test client mode is enabled
        var isTestClientModeEnabled = _settingsService.Settings.TestClient.Enabled;
        
        // Add to command queue (respect test client mode)
        var command = _commandQueue.AddCommand(
            entry.UserPrompt,
            executionCode,
            undoCode,
            entry.Source,
            entry.Author,
            entry.ImageUrl,
            entry.UserId,
            null,
            queueForExecution: !isTestClientModeEnabled);
        
        // If test client mode is enabled, queue for testing
        if (isTestClientModeEnabled)
        {
            _testClientService.QueueForTesting(command.Id, executionCode, entry.UserPrompt);
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
