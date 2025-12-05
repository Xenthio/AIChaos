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
    private readonly QueueSlotService _queueSlots;
    private readonly AiCodeGeneratorService _codeGenerator;
    private readonly SettingsService _settingsService;
    private readonly ImageModerationService _moderationService;
    private readonly TestClientService _testClientService;
    private readonly AgenticGameService _agenticService;
    private readonly ILogger<ChaosController> _logger;

    public ChaosController(
        CommandQueueService commandQueue,
        QueueSlotService queueSlots,
        AiCodeGeneratorService codeGenerator,
        SettingsService settingsService,
        ImageModerationService moderationService,
        TestClientService testClientService,
        AgenticGameService agenticService,
        ILogger<ChaosController> logger)
    {
        _commandQueue = commandQueue;
        _queueSlots = queueSlots;
        _codeGenerator = codeGenerator;
        _settingsService = settingsService;
        _moderationService = moderationService;
        _testClientService = testClientService;
        _agenticService = agenticService;
        _logger = logger;
    }

    /// <summary>
    /// Polls for the next command in the queue (called by GMod).
    /// If test client mode is enabled, only returns commands that passed testing.
    /// Supports both GET and POST for compatibility with various tunnel services.
    /// </summary>
    [HttpGet("poll")]
    [HttpPost("poll")]
    public ActionResult<PollResponse> Poll()
    {
        // Log incoming request for debugging
        _logger.LogDebug("Poll request received from {RemoteIp}", HttpContext.Connection.RemoteIpAddress);

        // Check for timed out tests
        _testClientService.CheckTimeouts();

        // Add ngrok bypass header in response
        Response.Headers.Append("ngrok-skip-browser-warning", "true");

        // If test client mode is enabled, check for approved commands first
        if (_testClientService.IsEnabled)
        {
            var approvedResult = _testClientService.PollApprovedCommand();
            if (approvedResult.HasValue)
            {
                _logger.LogInformation("[MAIN CLIENT] Sending approved command #{CommandId}", approvedResult.Value.CommandId);
                return new PollResponse
                {
                    HasCode = true,
                    Code = approvedResult.Value.Code,
                    CommandId = approvedResult.Value.CommandId
                };
            }

            // No approved commands, return empty
            return new PollResponse
            {
                HasCode = false,
                Code = null,
                CommandId = null
            };
        }

        // Test client mode is disabled, use queue slot service
        var result = _queueSlots.PollNextCommand();

        // Add ngrok bypass header in response (helps with some ngrok configurations)
        Response.Headers.Append("ngrok-skip-browser-warning", "true");

        if (result.HasValue)
        {
            return new PollResponse
            {
                HasCode = true,
                Code = result.Value.Code,
                CommandId = result.Value.CommandId
            };
        }

        return new PollResponse
        {
            HasCode = false,
            Code = null,
            CommandId = null
        };
    }

    /// <summary>
    /// Reports execution result from GMod (success or error).
    /// </summary>
    [HttpPost("report")]
    public async Task<ActionResult<ApiResponse>> ReportResult([FromBody] ExecutionResultRequest request)
    {
        // Check if this is an agentic session command (negative IDs)
        if (request.CommandId < 0)
        {
            var handled = await _agenticService.ReportResultAsync(
                request.CommandId,
                request.Success,
                request.Error,
                request.ResultData);

            if (handled)
            {
                _logger.LogInformation("[AGENTIC] Reported result for command #{CommandId}", request.CommandId);
                return Ok(new ApiResponse
                {
                    Status = "success",
                    Message = "Agentic result recorded",
                    CommandId = request.CommandId
                });
            }
        }

        if (request.CommandId <= 0)
        {
            return Ok(new ApiResponse { Status = "ignored", Message = "No command ID to report" });
        }

        // Check if this is a regular command that's also part of an agentic session
        var agenticHandled = await _agenticService.ReportResultAsync(
            request.CommandId,
            request.Success,
            request.Error,
            request.ResultData);

        if (_commandQueue.ReportExecutionResult(request.CommandId, request.Success, request.Error))
        {
            if (request.Success)
            {
                _logger.LogInformation("[EXECUTED] Command #{CommandId} executed successfully", request.CommandId);
            }
            else
            {
                _logger.LogWarning("[ERROR] Command #{CommandId} failed: {Error}", request.CommandId, request.Error);
            }

            return Ok(new ApiResponse
            {
                Status = "success",
                Message = request.Success ? "Execution recorded" : "Error recorded",
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
    /// Polls for the next command to test (called by test client GMod instance).
    /// </summary>
    [HttpGet("poll/test")]
    [HttpPost("poll/test")]
    public ActionResult<TestPollResponse> PollTest()
    {
        _logger.LogDebug("Test client poll received from {RemoteIp}", HttpContext.Connection.RemoteIpAddress);

        Response.Headers.Append("ngrok-skip-browser-warning", "true");

        var result = _testClientService.PollTestCommand();

        if (result != null)
        {
            return Ok(result);
        }

        // Return 204 No Content when there's nothing to test
        return NoContent();
    }

    /// <summary>
    /// Reports test result from test client GMod instance.
    /// If the test fails, AI will attempt to fix the code and retry.
    /// </summary>
    [HttpPost("report/test")]
    public async Task<ActionResult<ApiResponse>> ReportTestResult([FromBody] TestResultRequest request)
    {
        var action = await _testClientService.ReportTestResultAsync(request.CommandId, request.Success, request.Error);

        string message = action switch
        {
            TestResultAction.Approved => "Test passed - command queued for main client",
            TestResultAction.Rejected => "Test failed after max attempts - command will not be sent to main client",
            TestResultAction.Retrying => "Test failed - AI is fixing the code and will retry",
            _ => "Unknown command"
        };

        if (action == TestResultAction.Approved)
        {
            _logger.LogInformation("[TEST CLIENT] Command #{CommandId} approved", request.CommandId);
        }
        else if (action == TestResultAction.Rejected)
        {
            _logger.LogWarning("[TEST CLIENT] Command #{CommandId} rejected after all attempts: {Error}", request.CommandId, request.Error);
        }
        else if (action == TestResultAction.Retrying)
        {
            _logger.LogInformation("[TEST CLIENT] Command #{CommandId} failed, AI is fixing and will retry", request.CommandId);
        }

        return Ok(new ApiResponse
        {
            Status = action == TestResultAction.Unknown ? "error" : "success",
            Message = message,
            CommandId = request.CommandId
        });
    }
}
