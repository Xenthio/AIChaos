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
    private readonly ImageModerationService _moderationService;
    private readonly TestClientService _testClientService;
    private readonly AgenticGameService _agenticService;
    private readonly ILogger<ChaosController> _logger;

    public ChaosController(
        CommandQueueService commandQueue,
        AiCodeGeneratorService codeGenerator,
        SettingsService settingsService,
        ImageModerationService moderationService,
        TestClientService testClientService,
        AgenticGameService agenticService,
        ILogger<ChaosController> logger)
    {
        _commandQueue = commandQueue;
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

        // Test client mode is disabled, use normal queue
        var result = _commandQueue.PollNextCommand();

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
    /// Triggers an interactive AI session that can iterate with the game.
    /// This is a legacy endpoint that now uses the unified AgenticGameService.
    /// </summary>
    [HttpPost("trigger/interactive")]
    public async Task<ActionResult<InteractiveSessionResponse>> TriggerInteractive([FromBody] InteractiveTriggerRequest request)
    {
        if (string.IsNullOrEmpty(request.Prompt))
        {
            return BadRequest(new InteractiveSessionResponse
            {
                Status = "error",
                Message = "No prompt provided"
            });
        }

        _logger.LogInformation("[INTERACTIVE] Starting interactive session for: {Prompt}", request.Prompt);

        // Convert to AgentSessionRequest and use AgenticGameService
        var agentRequest = new AgentSessionRequest
        {
            Prompt = request.Prompt,
            UserId = request.UserId,
            MaxIterations = request.MaxIterations,
            UseTestClient = false // Interactive mode uses main client
        };

        var session = await _agenticService.CreateSessionAsync(agentRequest);

        return Ok(new InteractiveSessionResponse
        {
            Status = session.IsComplete ? (session.WasSuccessful ? "complete" : "failed") : "in_progress",
            Message = session.IsComplete
                ? (session.WasSuccessful ? "Session completed successfully" : "Session failed")
                : "Session started - waiting for game response",
            SessionId = session.Id,
            Iteration = session.CurrentIteration,
            CurrentPhase = session.CurrentPhase.ToString(),
            IsComplete = session.IsComplete,
            FinalCode = session.FinalExecutionCode,
            Steps = session.Steps.Select(s => new InteractionStep
            {
                StepNumber = s.StepNumber,
                Phase = s.Phase,
                Code = s.Code,
                Success = s.Success,
                Error = s.Error,
                ResultData = s.ResultData,
                AiThinking = s.AiThinking
            }).ToList()
        });
    }

    /// <summary>
    /// Gets the status of an interactive session.
    /// This is a legacy endpoint that now uses the unified AgenticGameService.
    /// </summary>
    [HttpGet("api/interactive/{sessionId}")]
    public ActionResult<InteractiveSessionResponse> GetInteractiveSession(int sessionId)
    {
        var session = _agenticService.GetSession(sessionId);

        if (session == null)
        {
            return NotFound(new InteractiveSessionResponse
            {
                Status = "error",
                Message = "Session not found"
            });
        }

        return Ok(new InteractiveSessionResponse
        {
            Status = session.IsComplete ? (session.WasSuccessful ? "complete" : "failed") : "in_progress",
            SessionId = session.Id,
            Iteration = session.CurrentIteration,
            CurrentPhase = session.CurrentPhase.ToString(),
            IsComplete = session.IsComplete,
            FinalCode = session.FinalExecutionCode,
            Steps = session.Steps.Select(s => new InteractionStep
            {
                StepNumber = s.StepNumber,
                Phase = s.Phase,
                Code = s.Code,
                Success = s.Success,
                Error = s.Error,
                ResultData = s.ResultData,
                AiThinking = s.AiThinking
            }).ToList()
        });
    }

    /// <summary>
    /// Gets all active interactive sessions.
    /// This is a legacy endpoint that now uses the unified AgenticGameService.
    /// </summary>
    [HttpGet("api/interactive/active")]
    public ActionResult<object> GetActiveInteractiveSessions()
    {
        var sessions = _agenticService.GetActiveSessions();

        return Ok(new
        {
            count = sessions.Count,
            sessions = sessions.Select(s => new
            {
                sessionId = s.Id,
                prompt = s.UserPrompt,
                phase = s.CurrentPhase.ToString(),
                iteration = s.CurrentIteration,
                maxIterations = s.MaxIterations,
                createdAt = s.CreatedAt
            })
        });
    }

    // ==========================================
    // UNIFIED AGENTIC GAME SERVICE ENDPOINTS
    // ==========================================

    /// <summary>
    /// Triggers an agentic AI session that can iterate with the game.
    /// Supports both main client and test client modes.
    /// </summary>
    [HttpPost("trigger/agent")]
    public async Task<ActionResult<AgentSessionResponse>> TriggerAgentSession([FromBody] AgentSessionRequest request)
    {
        if (string.IsNullOrEmpty(request.Prompt))
        {
            return BadRequest(new AgentSessionResponse
            {
                Status = "error",
                Message = "No prompt provided"
            });
        }

        _logger.LogInformation("[AGENT] Starting agentic session for: {Prompt} (UseTestClient: {UseTestClient})",
            request.Prompt, request.UseTestClient);

        var session = await _agenticService.CreateSessionAsync(request);

        return Ok(new AgentSessionResponse
        {
            Status = session.IsComplete ? (session.WasSuccessful ? "complete" : "failed") : "in_progress",
            Message = session.IsComplete
                ? (session.WasSuccessful ? "Session completed successfully" : "Session failed")
                : "Session started - waiting for game response",
            SessionId = session.Id,
            Mode = session.Mode.ToString(),
            Iteration = session.CurrentIteration,
            CurrentPhase = session.CurrentPhase.ToString(),
            IsComplete = session.IsComplete,
            FinalCode = session.FinalExecutionCode,
            Steps = session.Steps.Select(s => new AgentStepResponse
            {
                StepNumber = s.StepNumber,
                Phase = s.Phase,
                Code = s.Code,
                Success = s.Success,
                Error = s.Error,
                ResultData = s.ResultData,
                AiThinking = s.AiThinking
            }).ToList()
        });
    }

    /// <summary>
    /// Gets the status of an agentic session.
    /// </summary>
    [HttpGet("api/agent/{sessionId}")]
    public ActionResult<AgentSessionResponse> GetAgentSession(int sessionId)
    {
        var session = _agenticService.GetSession(sessionId);

        if (session == null)
        {
            return NotFound(new AgentSessionResponse
            {
                Status = "error",
                Message = "Session not found"
            });
        }

        return Ok(new AgentSessionResponse
        {
            Status = session.IsComplete ? (session.WasSuccessful ? "complete" : "failed") : "in_progress",
            SessionId = session.Id,
            Mode = session.Mode.ToString(),
            Iteration = session.CurrentIteration,
            CurrentPhase = session.CurrentPhase.ToString(),
            IsComplete = session.IsComplete,
            FinalCode = session.FinalExecutionCode,
            Steps = session.Steps.Select(s => new AgentStepResponse
            {
                StepNumber = s.StepNumber,
                Phase = s.Phase,
                Code = s.Code,
                Success = s.Success,
                Error = s.Error,
                ResultData = s.ResultData,
                AiThinking = s.AiThinking
            }).ToList()
        });
    }

    /// <summary>
    /// Gets all active agentic sessions.
    /// </summary>
    [HttpGet("api/agent/active")]
    public ActionResult<object> GetActiveAgentSessions()
    {
        var sessions = _agenticService.GetActiveSessions();

        return Ok(new
        {
            count = sessions.Count,
            testClientConnected = _agenticService.IsTestClientConnected,
            testClientEnabled = _agenticService.IsTestClientEnabled,
            sessions = sessions.Select(s => new
            {
                sessionId = s.Id,
                prompt = s.UserPrompt,
                mode = s.Mode.ToString(),
                phase = s.CurrentPhase.ToString(),
                iteration = s.CurrentIteration,
                maxIterations = s.MaxIterations,
                createdAt = s.CreatedAt
            })
        });
    }

    /// <summary>
    /// Gets the test queue status from the agentic service.
    /// </summary>
    [HttpGet("api/agent/queue")]
    public ActionResult<TestQueueStatus> GetAgentQueueStatus()
    {
        return Ok(_agenticService.GetQueueStatus());
    }
    /// <summary>
    /// Gets public authentication configuration (Client IDs).
    /// </summary>
    [HttpGet("api/auth/config")]
    public ActionResult GetAuthConfig()
    {
        return Ok(new
        {
            googleClientId = _settingsService.Settings.YouTube.ClientId
        });
    }
}
