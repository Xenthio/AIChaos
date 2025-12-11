using AIChaos.Brain.Models;
using AIChaos.Brain.Services;
using Microsoft.AspNetCore.Mvc;

namespace AIChaos.Brain.Controllers;

/// <summary>
/// Controller for public-facing refund request endpoint.
/// Image moderation and admin refund management use service injection directly in UI components.
/// </summary>
[ApiController]
[Route("api/moderation")]
public class ModerationController : ControllerBase
{
    private readonly CommandQueueService _commandQueue;
    private readonly RefundService _refundService;
    private readonly ILogger<ModerationController> _logger;
    
    // Reasons that trigger a real refund request (others show fake "Submitted" success)
    private static readonly string[] RealRefundReasons = new[]
    {
        "My request didn't work",
        "The streamer didn't see my request"
    };
    
    public ModerationController(
        CommandQueueService commandQueue,
        RefundService refundService,
        ILogger<ModerationController> logger)
    {
        _commandQueue = commandQueue;
        _refundService = refundService;
        _logger = logger;
    }
    
    /// <summary>
    /// Submits a refund request. Public endpoint (no auth required).
    /// For "fake" reasons, just returns success without creating a real request.
    /// Admin/moderator refund management uses service injection directly in UI components.
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
        
        if (refundRequest == null)
        {
            return BadRequest(new { 
                status = "error", 
                message = "Could not create refund request. This command may have already been refunded." 
            });
        }
        
        return Ok(new { 
            status = "success", 
            message = "Your refund request has been submitted for review.",
            requestId = refundRequest.Id
        });
    }
}
