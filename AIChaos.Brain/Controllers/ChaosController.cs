using AIChaos.Brain.Models;
using AIChaos.Brain.Services;
using Microsoft.AspNetCore.Mvc;

namespace AIChaos.Brain.Controllers;

/// <summary>
/// Controller for the main chaos API endpoints.
/// </summary>
[ApiController]
[Route("")]
public class ChaosController : ControllerBase
{
    private readonly CommandQueueService _commandQueue;
    private readonly AiCodeGeneratorService _codeGenerator;
    private readonly SettingsService _settingsService;
    private readonly ILogger<ChaosController> _logger;
    
    public ChaosController(
        CommandQueueService commandQueue,
        AiCodeGeneratorService codeGenerator,
        SettingsService settingsService,
        ILogger<ChaosController> logger)
    {
        _commandQueue = commandQueue;
        _codeGenerator = codeGenerator;
        _settingsService = settingsService;
        _logger = logger;
    }
    
    /// <summary>
    /// Polls for the next command in the queue (called by GMod).
    /// Supports both GET and POST for compatibility with various tunnel services.
    /// </summary>
    [HttpGet("poll")]
    [HttpPost("poll")]
    public ActionResult<PollResponse> Poll()
    {
        // Log incoming request for debugging
        _logger.LogDebug("Poll request received from {RemoteIp}", HttpContext.Connection.RemoteIpAddress);
        
        var code = _commandQueue.PollNextCommand();
        
        // Add ngrok bypass header in response (helps with some ngrok configurations)
        Response.Headers.Append("ngrok-skip-browser-warning", "true");
        
        return new PollResponse
        {
            HasCode = code != null,
            Code = code
        };
    }
    
    /// <summary>
    /// Triggers a new chaos command.
    /// </summary>
    [HttpPost("trigger")]
    public async Task<ActionResult<TriggerResponse>> Trigger([FromBody] TriggerRequest request)
    {
        if (string.IsNullOrEmpty(request.Prompt))
        {
            return BadRequest(new TriggerResponse
            {
                Status = "error",
                Message = "No prompt provided"
            });
        }
        
        // Check for changelevel attempts
        var changeLevelKeywords = new[] { "changelevel", "change level", "next map", "load map", "switch map", "new map" };
        if (changeLevelKeywords.Any(k => request.Prompt.Contains(k, StringComparison.OrdinalIgnoreCase)))
        {
            _logger.LogWarning("[SAFETY] Blocked changelevel attempt: {Prompt}", request.Prompt);
            return Ok(new TriggerResponse
            {
                Status = "ignored",
                Message = "Map/level changing is blocked for safety."
            });
        }
        
        _logger.LogInformation("Generating code for: {Prompt}", request.Prompt);
        
        // Generate code
        var (executionCode, undoCode) = await _codeGenerator.GenerateCodeAsync(request.Prompt);
        
        // Post-generation safety check
        var dangerousPatterns = new[] { "changelevel", "RunConsoleCommand.*\"map\"", "game.ConsoleCommand.*map" };
        if (dangerousPatterns.Any(p => 
            System.Text.RegularExpressions.Regex.IsMatch(executionCode, p, 
                System.Text.RegularExpressions.RegexOptions.IgnoreCase)))
        {
            _logger.LogWarning("[SAFETY] AI tried to generate changelevel code - blocking!");
            return Ok(new TriggerResponse
            {
                Status = "ignored",
                Message = "Generated code attempted to change map (blocked)."
            });
        }
        
        // Add to queue and history
        var entry = _commandQueue.AddCommand(
            request.Prompt,
            executionCode,
            undoCode,
            request.Source ?? "web",
            request.Author ?? "anonymous");
        
        return Ok(new TriggerResponse
        {
            Status = "queued",
            CodePreview = executionCode,
            HasUndo = true,
            CommandId = entry.Id,
            WasBlocked = false
        });
    }
    
    /// <summary>
    /// Gets command history and preferences.
    /// </summary>
    [HttpGet("api/history")]
    public ActionResult<HistoryResponse> GetHistory()
    {
        return Ok(new HistoryResponse
        {
            History = _commandQueue.GetHistory(),
            Preferences = _commandQueue.Preferences
        });
    }
    
    /// <summary>
    /// Repeats a previous command.
    /// </summary>
    [HttpPost("api/repeat")]
    public ActionResult<ApiResponse> RepeatCommand([FromBody] CommandIdRequest request)
    {
        if (_commandQueue.RepeatCommand(request.CommandId))
        {
            _logger.LogInformation("[REPEAT] Re-executing command #{CommandId}", request.CommandId);
            return Ok(new ApiResponse
            {
                Status = "success",
                Message = "Command re-queued for execution",
                CommandId = request.CommandId
            });
        }
        
        return NotFound(new ApiResponse
        {
            Status = "error",
            Message = "Command not found in history"
        });
    }
    
    /// <summary>
    /// Undoes a previous command.
    /// </summary>
    [HttpPost("api/undo")]
    public ActionResult<ApiResponse> UndoCommand([FromBody] CommandIdRequest request)
    {
        if (_commandQueue.UndoCommand(request.CommandId))
        {
            _logger.LogInformation("[UNDO] Executing undo for command #{CommandId}", request.CommandId);
            return Ok(new ApiResponse
            {
                Status = "success",
                Message = "Undo code queued for execution",
                CommandId = request.CommandId
            });
        }
        
        return NotFound(new ApiResponse
        {
            Status = "error",
            Message = "Command not found in history"
        });
    }
    
    /// <summary>
    /// Force undoes a command using AI.
    /// </summary>
    [HttpPost("api/force_undo")]
    public async Task<ActionResult<ApiResponse>> ForceUndoCommand([FromBody] CommandIdRequest request)
    {
        var command = _commandQueue.GetCommand(request.CommandId);
        if (command == null)
        {
            return NotFound(new ApiResponse
            {
                Status = "error",
                Message = "Command not found in history"
            });
        }
        
        _logger.LogInformation("[FORCE UNDO] Generating AI solution for command #{CommandId}", request.CommandId);
        
        var forceUndoCode = await _codeGenerator.GenerateForceUndoAsync(command);
        _commandQueue.QueueCode(forceUndoCode);
        
        return Ok(new ApiResponse
        {
            Status = "success",
            Message = "AI-generated force undo queued",
            CommandId = request.CommandId
        });
    }
    
    /// <summary>
    /// Updates user preferences.
    /// </summary>
    [HttpPost("api/preferences")]
    public ActionResult<object> UpdatePreferences([FromBody] UserPreferences prefs)
    {
        _commandQueue.Preferences.IncludeHistoryInAi = prefs.IncludeHistoryInAi;
        _commandQueue.Preferences.HistoryEnabled = prefs.HistoryEnabled;
        _commandQueue.Preferences.MaxHistoryLength = prefs.MaxHistoryLength;
        
        _logger.LogInformation("[PREFERENCES] Updated preferences");
        
        return Ok(new
        {
            status = "success",
            preferences = _commandQueue.Preferences
        });
    }
    
    /// <summary>
    /// Clears command history.
    /// </summary>
    [HttpPost("api/clear_history")]
    public ActionResult<ApiResponse> ClearHistory()
    {
        _commandQueue.ClearHistory();
        _logger.LogInformation("[HISTORY] Command history cleared");
        
        return Ok(new ApiResponse
        {
            Status = "success",
            Message = "History cleared"
        });
    }
    
    /// <summary>
    /// Saves a command payload for random chaos mode.
    /// </summary>
    [HttpPost("api/save_payload")]
    public ActionResult<ApiResponse> SavePayload([FromBody] SavePayloadRequest request)
    {
        var command = _commandQueue.GetCommand(request.CommandId);
        if (command == null)
        {
            return NotFound(new ApiResponse
            {
                Status = "error",
                Message = "Command not found in history"
            });
        }
        
        var payload = _commandQueue.SavePayload(command, request.Name);
        _logger.LogInformation("[SAVED PAYLOAD] Saved command #{CommandId} as '{Name}'", request.CommandId, request.Name);
        
        return Ok(new ApiResponse
        {
            Status = "success",
            Message = $"Payload saved as '{payload.Name}'",
            CommandId = payload.Id
        });
    }
    
    /// <summary>
    /// Gets all saved payloads.
    /// </summary>
    [HttpGet("api/saved_payloads")]
    public ActionResult<SavedPayloadsResponse> GetSavedPayloads()
    {
        return Ok(new SavedPayloadsResponse
        {
            Payloads = _commandQueue.GetSavedPayloads()
        });
    }
    
    /// <summary>
    /// Deletes a saved payload.
    /// </summary>
    [HttpPost("api/delete_payload")]
    public ActionResult<ApiResponse> DeletePayload([FromBody] DeletePayloadRequest request)
    {
        if (_commandQueue.DeletePayload(request.PayloadId))
        {
            _logger.LogInformation("[SAVED PAYLOAD] Deleted payload #{PayloadId}", request.PayloadId);
            return Ok(new ApiResponse
            {
                Status = "success",
                Message = "Payload deleted"
            });
        }
        
        return NotFound(new ApiResponse
        {
            Status = "error",
            Message = "Payload not found"
        });
    }
}
