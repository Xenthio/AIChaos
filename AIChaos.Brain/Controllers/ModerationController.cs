using AIChaos.Brain.Models;
using AIChaos.Brain.Services;
using Microsoft.AspNetCore.Mvc;

namespace AIChaos.Brain.Controllers;

/// <summary>
/// Controller for image moderation endpoints.
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
    private readonly ILogger<ModerationController> _logger;
    
    public ModerationController(
        ImageModerationService moderationService,
        CommandQueueService commandQueue,
        AiCodeGeneratorService codeGenerator,
        SettingsService settingsService,
        ILogger<ModerationController> logger)
    {
        _moderationService = moderationService;
        _commandQueue = commandQueue;
        _codeGenerator = codeGenerator;
        _settingsService = settingsService;
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
        
        // Add to command queue
        var command = _commandQueue.AddCommand(
            entry.UserPrompt,
            executionCode,
            undoCode,
            entry.Source,
            entry.Author,
            entry.ImageUrl,
            entry.UserId);
        
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
}
