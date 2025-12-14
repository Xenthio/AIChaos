using System.Text;
using System.Text.Json;
using AIChaos.Brain.Models;

namespace AIChaos.Brain.Services;

/// <summary>
/// Unified agentic service for AI-driven game interaction.
/// Combines interactive AI sessions with test client functionality.
/// 
/// Execution modes:
/// - Direct: Code goes straight to main client (legacy mode)
/// - Interactive: AI can gather info, iterate, and fix errors on main client
/// - TestClient: Code is tested on a separate GMod instance before main client
/// - AgenticTest: Full AI-driven testing with prepare/generate/fix phases on test client
/// </summary>
public class AgenticGameService
{
    private readonly SettingsService _settingsService;
    private readonly CommandQueueService _commandQueue;
    private readonly CodeModerationService _codeModerationService;
    private readonly OpenRouterService _openRouterService;
    private readonly ILogger<AgenticGameService> _logger;
    
    // Active agent sessions
    private readonly Dictionary<int, AgentSession> _sessions = new();
    private readonly object _lock = new();
    private int _nextSessionId = 1;
    
    // Test client queues (when using test client mode)
    private readonly List<TestQueueItem> _testQueue = new();
    private readonly List<(int CommandId, string Code)> _approvedQueue = new();
    private readonly Dictionary<int, PendingExecution> _pendingExecutions = new();
    
    private const int MaxIterations = 5;
    private const int MaxFixAttempts = 3;
    
    #region System Prompts
    
    // Use the shared ground rules from AiCodeGeneratorService
    private static string AgenticSystemPrompt => $$"""
        You are an expert Lua scripter for Garry's Mod (GLua) with the ability to interact with the game iteratively.
        You will receive a request from a livestream chat and can optionally execute preparation code to gather information before generating your final code. 
        The chat is controlling the streamer's playthrough of Half-Life 2 via your generated scripts.

        **CRITICAL: YOU MUST ALWAYS RESPOND WITH VALID JSON!**
        Your response MUST be a valid JSON object. Do not include any text before or after the JSON.
        Do not use markdown. Do not add explanations outside the JSON object.
        
        **IMPORTANT: You can skip preparation and generate immediately!**
        If the request is simple or you already know how to do it, set `isComplete: true` and provide the code directly.
        Only use preparation phases when you genuinely need to discover something (find specific models, check entity state, etc.)
        
        **INTERACTION PHASES:**
        1. **PREPARE** (optional) - Run code to gather information (search models/textures/other assets, check entities, get player state), note you cannot ask for user clarification. If you're unsure about file paths, use this to find them!
        2. **GENERATE** - Generate the main execution code (can be done immediately if no prep needed)
        3. **FIX** - If execution fails, analyze errors and fix the code
        
        **RESPONSE FORMAT (REQUIRED):**
        You MUST respond with ONLY this JSON object, nothing else:
        ```json
        {
            "phase": "prepare|generate|fix",
            "thinking": "Your reasoning about what to do",
            "code": "The Lua code to execute",
            "undoCode": "Undo code (only when isComplete is true)",
            "isComplete": true/false,
            "searchQuery": "optional: what you're searching for"
        }
        ```
        
        **JSON REQUIREMENTS:**
        - phase: Must be one of "prepare", "generate", or "fix"
        - thinking: Brief explanation of your reasoning (1-2 sentences)
        - code: The Lua code to execute (required)
        - undoCode: Undo code (only include if isComplete is true)
        - isComplete: true if this is the final code, false if you need more iterations
        - searchQuery: (optional) what you're searching for in preparation
        
        **WHEN TO SET isComplete: true (SKIP PREPARATION):**
        - Simple effects like gravity, speed, scale changes
        - Spawning common entities (props, NPCs, vehicles)
        - Screen effects, UI overlays, chat messages
        - Timer-based effects
        - Anything where you don't need to discover game state first
        
        **WHEN TO USE PREPARATION (isComplete: false):**
        - Need to find specific models that exist in the game
        - Need to check player inventory or current state
        - Need to find specific entities by class or property
        - Complex effects that depend on game state
        
        **PREPARATION CODE EXAMPLES (when needed):**
        - Search for models: `local models = {} for _, ent in pairs(ents.GetAll()) do local m = ent:GetModel() if m and m:find("pattern") then table.insert(models, m) end end PrintTable(models)`
        - Find NPCs: `for _, npc in pairs(ents.FindByClass("npc_*")) do print(npc:GetClass(), npc:GetPos()) end`
        - Check player state: `local p = Entity(1) print("Health:", p:Health(), "Pos:", p:GetPos(), "Weapon:", p:GetActiveWeapon():GetClass())`
        - **Workshop content inspection**: `DownloadAndGetWorkshopAssets("workshopId", function(assets) if assets then print("Models:", #assets.models, "Weapons:", #assets.weapons, "Entities:", #assets.entities) PrintTable(assets) end end)`
        
        **WORKSHOP CONTENT IN AGENTIC MODE:**
        Use `DownloadAndGetWorkshopAssets()` in preparation phase to discover what's in a workshop addon:
        1. **Prep phase**: Download and inspect - `DownloadAndGetWorkshopAssets("id", function(a) PrintTable(a) end)`
        2. **Generate phase**: Use discovered weapons/entities/models appropriately
        
        Example workflow:
        - User: "download workshop 158421055"
        - Prep: Use DownloadAndGetWorkshopAssets to discover it has weapons
        - Generate: Use the weapon class names to give to player with ply:Give()
        
        **AGENTIC WORKFLOW RULES:**
        1. **BE EFFICIENT** - If you can generate immediately, do so with `isComplete: true`
        2. Preparation code should use print() or PrintTable() to output data that will be returned to you
        3. Keep preparation code focused and minimal
        4. After getting preparation results, generate the main code with full context
        5. If the main code fails, analyze the error and generate fixed code
        6. Maximum iterations are limited - don't waste them on unnecessary preparation
        
        **REMEMBER: Your entire response must be valid JSON. Nothing before the opening {, nothing after the closing }.**
        
        {{AiCodeGeneratorService.GroundRules}}
        """;
    
    private const string ErrorFixSystemPrompt = """
        You are an expert Lua scripter for Garry's Mod (GLua).
        You will be given code that failed to execute, along with the error message.
        Your job is to fix the code so it runs without errors.
        
        **IMPORTANT RULES:**
        1. Analyze the error message carefully to understand what went wrong
        2. Fix ONLY the issue causing the error - don't change working code
        3. Common GLua errors and fixes:
           - "attempt to call nil value" - the function doesn't exist, use correct function name
           - "attempt to index nil value" - checking for nil before accessing
           - "attempt to perform arithmetic on" - type conversion issues
           - "bad argument" - wrong argument type or count
           - Syntax errors - check brackets, quotes, commas
           - Code execution failed (compilation or runtime error) - This is an unknown error, it's possible a function doesnt exist, or a function used isn't available on the realm you're attempting to run it on.
        
        4. Return ONLY the fixed raw Lua code. No markdown, no explanations.
        5. If the code references a function like RunOnClient, make sure it exists in the environment
        6. The code runs on SERVER side. Use player.GetAll() or Entity(1) for players, not LocalPlayer()
        
        **OUTPUT FORMAT:**
        Return ONLY the fixed Lua code with no markdown backticks or explanations.
        """;
    
    #endregion
    
    public AgenticGameService(
        SettingsService settingsService,
        CommandQueueService commandQueue,
        CodeModerationService codeModerationService,
        OpenRouterService openRouterService,
        ILogger<AgenticGameService> logger)
    {
        _settingsService = settingsService;
        _commandQueue = commandQueue;
        _codeModerationService = codeModerationService;
        _openRouterService = openRouterService;
        _logger = logger;
    }
    
    #region Properties
    
    /// <summary>
    /// Whether test client mode is enabled.
    /// </summary>
    public bool IsTestClientEnabled => _settingsService.Settings.TestClient.Enabled;
    
    /// <summary>
    /// Whether test client is currently connected.
    /// </summary>
    public bool IsTestClientConnected
    {
        get
        {
            var lastPoll = _settingsService.Settings.TestClient.LastPollTime;
            if (!lastPoll.HasValue) return false;
            return (DateTime.UtcNow - lastPoll.Value).TotalSeconds < 30;
        }
    }
    
    #endregion
    
    #region Session Management
    
    /// <summary>
    /// Creates a new agentic session for interactive game interaction.
    /// </summary>
    public async Task<AgentSession> CreateSessionAsync(AgentSessionRequest request)
    {
        var session = new AgentSession
        {
            Id = GetNextSessionId(),
            UserPrompt = request.Prompt,
            Source = request.Source ?? "web",
            Author = request.Author ?? "anonymous",
            UserId = request.UserId,
            MaxIterations = request.MaxIterations > 0 ? request.MaxIterations : MaxIterations,
            Mode = request.UseTestClient && IsTestClientEnabled ? AgentMode.AgenticTest : AgentMode.Interactive,
            CurrentPhase = AgentPhase.Preparing
        };
        
        lock (_lock)
        {
            _sessions[session.Id] = session;
        }
        
        _logger.LogInformation("[AGENT] Created session #{SessionId} (mode: {Mode}) for: {Prompt}", 
            session.Id, session.Mode, request.Prompt);
        
        // Start the first iteration
        await ProcessIterationAsync(session);
        
        return session;
    }
    
    /// <summary>
    /// Gets a session by ID.
    /// </summary>
    public AgentSession? GetSession(int sessionId)
    {
        lock (_lock)
        {
            return _sessions.TryGetValue(sessionId, out var session) ? session : null;
        }
    }
    
    /// <summary>
    /// Gets all active sessions.
    /// </summary>
    public List<AgentSession> GetActiveSessions()
    {
        lock (_lock)
        {
            return _sessions.Values.Where(s => !s.IsComplete).ToList();
        }
    }
    
    /// <summary>
    /// Gets all sessions (including completed ones).
    /// </summary>
    public List<AgentSession> GetAllSessions()
    {
        lock (_lock)
        {
            return _sessions.Values.ToList();
        }
    }
    
    private int GetNextSessionId()
    {
        lock (_lock)
        {
            return _nextSessionId++;
        }
    }
    
    #endregion
    
    #region Test Client Integration
    
    /// <summary>
    /// Queues a command for testing on the test client.
    /// </summary>
    public void QueueForTesting(int commandId, string code, string originalPrompt = "")
    {
        lock (_lock)
        {
            if (!IsTestClientEnabled)
            {
                _approvedQueue.Add((commandId, code));
                return;
            }
            
            var settings = _settingsService.Settings.TestClient;
            _testQueue.Add(new TestQueueItem
            {
                CommandId = commandId,
                OriginalPrompt = originalPrompt,
                CurrentCode = code,
                CleanupAfterTest = settings.CleanupAfterTest,
                AttemptCount = 0
            });
            _logger.LogInformation("[AGENT] Command #{CommandId} queued for testing", commandId);
        }
    }
    
    /// <summary>
    /// Polls for the next command to test (called by test client).
    /// </summary>
    public TestPollResponse? PollTestCommand()
    {
        lock (_lock)
        {
            _settingsService.UpdateTestClientConnection(true);
            
            if (_testQueue.Count == 0)
            {
                return null;
            }
            
            var item = _testQueue[0];
            _testQueue.RemoveAt(0);
            
            _pendingExecutions[item.CommandId] = new PendingExecution
            {
                CommandId = item.CommandId,
                OriginalPrompt = item.OriginalPrompt,
                CurrentCode = item.CurrentCode,
                StartedAt = DateTime.UtcNow,
                TimeoutSeconds = _settingsService.Settings.TestClient.TimeoutSeconds,
                AttemptCount = item.AttemptCount + 1,
                CleanupAfterTest = item.CleanupAfterTest,
                IsTestClient = true
            };
            
            _logger.LogInformation("[AGENT] Sending command #{CommandId} to test client (attempt {Attempt}/{Max})", 
                item.CommandId, item.AttemptCount + 1, MaxFixAttempts);
            
            return new TestPollResponse
            {
                HasCode = true,
                Code = item.CurrentCode,
                CommandId = item.CommandId,
                CleanupAfterTest = item.CleanupAfterTest,
                AttemptNumber = item.AttemptCount + 1,
                MaxAttempts = MaxFixAttempts
            };
        }
    }
    
    /// <summary>
    /// Polls for the next approved command (for main client).
    /// </summary>
    public (int CommandId, string Code)? PollApprovedCommand()
    {
        lock (_lock)
        {
            if (_approvedQueue.Count == 0) return null;
            var result = _approvedQueue[0];
            _approvedQueue.RemoveAt(0);
            return result;
        }
    }
    
    /// <summary>
    /// Reports execution result from test client.
    /// </summary>
    public async Task<TestResultAction> ReportTestResultAsync(int commandId, bool success, string? error)
    {
        PendingExecution? pending;
        
        lock (_lock)
        {
            if (!_pendingExecutions.TryGetValue(commandId, out pending))
            {
                _logger.LogWarning("[AGENT] Received result for unknown command #{CommandId}", commandId);
                return TestResultAction.Unknown;
            }
            _pendingExecutions.Remove(commandId);
        }
        
        if (success)
        {
            lock (_lock)
            {
                _approvedQueue.Add((commandId, pending.CurrentCode));
            }
            _logger.LogInformation("[AGENT] Command #{CommandId} PASSED testing - queued for main client", commandId);
            return TestResultAction.Approved;
        }
        else
        {
            if (pending.AttemptCount < MaxFixAttempts)
            {
                _logger.LogInformation("[AGENT] Command #{CommandId} failed, asking AI to fix...", commandId);
                
                try
                {
                    var fixedCode = await AskAiToFixCodeAsync(pending.CurrentCode, error ?? "Unknown error", pending.OriginalPrompt);
                    
                    if (!string.IsNullOrEmpty(fixedCode) && fixedCode != pending.CurrentCode)
                    {
                        lock (_lock)
                        {
                            _testQueue.Insert(0, new TestQueueItem
                            {
                                CommandId = commandId,
                                OriginalPrompt = pending.OriginalPrompt,
                                CurrentCode = fixedCode,
                                CleanupAfterTest = pending.CleanupAfterTest,
                                AttemptCount = pending.AttemptCount
                            });
                        }
                        
                        _logger.LogInformation("[AGENT] AI provided fix for command #{CommandId}", commandId);
                        return TestResultAction.Retrying;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[AGENT] Failed to get AI fix for command #{CommandId}", commandId);
                }
            }
            
            _logger.LogWarning("[AGENT] Command #{CommandId} FAILED after {Attempts} attempts", commandId, pending.AttemptCount);
            return TestResultAction.Rejected;
        }
    }
    
    /// <summary>
    /// Check for timed out executions.
    /// </summary>
    public void CheckTimeouts()
    {
        lock (_lock)
        {
            var now = DateTime.UtcNow;
            var timedOut = _pendingExecutions
                .Where(kvp => (now - kvp.Value.StartedAt).TotalSeconds > kvp.Value.TimeoutSeconds)
                .Select(kvp => kvp.Key)
                .ToList();
            
            foreach (var commandId in timedOut)
            {
                _pendingExecutions.Remove(commandId);
                _logger.LogWarning("[AGENT] Command #{CommandId} timed out", commandId);
            }
        }
    }
    
    /// <summary>
    /// Gets test queue status.
    /// </summary>
    public TestQueueStatus GetQueueStatus()
    {
        lock (_lock)
        {
            return new TestQueueStatus
            {
                QueuedCount = _testQueue.Count,
                PendingCount = _pendingExecutions.Count,
                ApprovedCount = _approvedQueue.Count,
                IsConnected = IsTestClientConnected
            };
        }
    }
    
    #endregion
    
    #region Interactive Session Processing
    
    /// <summary>
    /// Reports execution result for an interactive session command.
    /// Alias for ReportSessionResultAsync for backward compatibility.
    /// </summary>
    public async Task<bool> ReportResultAsync(int commandId, bool success, string? error, string? resultData)
    {
        return await ReportSessionResultAsync(commandId, success, error, resultData);
    }
    
    /// <summary>
    /// Reports execution result for an interactive session command.
    /// </summary>
    public async Task<bool> ReportSessionResultAsync(int commandId, bool success, string? error, string? resultData)
    {
        AgentSession? session = null;
        
        lock (_lock)
        {
            session = _sessions.Values.FirstOrDefault(s => s.PendingCommandId == commandId);
        }
        
        if (session == null) return false;
        
        var lastStep = session.Steps.LastOrDefault();
        if (lastStep != null)
        {
            lastStep.Success = success;
            lastStep.Error = error;
            lastStep.ResultData = resultData;
        }
        
        session.PendingCommandId = null;
        session.PendingCode = null;
        
        _logger.LogInformation("[AGENT] Session #{SessionId} received result - Success: {Success}", session.Id, success);
        
        if (success)
        {
            if (session.CurrentPhase == AgentPhase.Preparing)
            {
                await ProcessIterationAsync(session, resultData);
            }
            else if (session.CurrentPhase == AgentPhase.Generating || 
                     session.CurrentPhase == AgentPhase.Fixing ||
                     session.CurrentPhase == AgentPhase.Testing)
            {
                CompleteSession(session, true);
            }
        }
        else
        {
            if (session.CurrentIteration < session.MaxIterations)
            {
                session.CurrentPhase = AgentPhase.Fixing;
                await ProcessIterationAsync(session, error: error);
            }
            else
            {
                CompleteSession(session, false);
            }
        }
        
        return true;
    }
    
    private async Task ProcessIterationAsync(AgentSession session, string? previousResult = null, string? error = null)
    {
        if (session.IsComplete || session.CurrentIteration >= session.MaxIterations)
        {
            CompleteSession(session, session.WasSuccessful);
            return;
        }
        
        session.CurrentIteration++;
        
        try
        {
            var response = await CallAgentAiAsync(session, previousResult, error);
            
            if (response == null)
            {
                _logger.LogError("[AGENT] AI response was null for session #{SessionId}", session.Id);
                CompleteSession(session, false);
                return;
            }
            
            var step = new AgentStep
            {
                StepNumber = session.CurrentIteration,
                Phase = response.Phase ?? "unknown",
                Code = response.Code,
                AiThinking = response.Thinking
            };
            session.Steps.Add(step);
            
            if (response.IsComplete)
            {
                session.FinalExecutionCode = response.Code;
                session.FinalUndoCode = response.UndoCode;
                
                var codeToQueue = response.Code ?? "";
                var undoToQueue = response.UndoCode ?? "print(\"Undo not available\")";
                
                // Apply post-code generation filters (same as regular code generation)
                // Check for dangerous patterns first (always block these)
                var dangerousReason = CodeModerationService.GetDangerousPatternReason(codeToQueue);
                if (dangerousReason != null)
                {
                    _logger.LogWarning("[AGENT] Session #{SessionId} generated dangerous code: {Reason}. Blocking.", 
                        session.Id, dangerousReason);
                    
                    codeToQueue = $"print(\"[BLOCKED] This command would break the game: {dangerousReason}\")";
                    undoToQueue = "print(\"No undo needed - command was blocked\")";
                    
                    // Queue the blocked message
                    var blockedEntry = _commandQueue.AddCommand(
                        session.UserPrompt,
                        codeToQueue,
                        undoToQueue,
                        session.Source,
                        session.Author,
                        null,
                        session.UserId);
                    
                    session.PendingCommandId = blockedEntry.Id;
                    CompleteSession(session, false);
                    return;
                }
                
                // Check for filtered patterns (send to moderation if BlockLinksInGeneratedCode is enabled)
                var settings = _settingsService.Settings;
                string? moderationReason = null;
                if (settings.General.BlockLinksInGeneratedCode)
                {
                    moderationReason = CodeModerationService.GetFilteredPatternReason(codeToQueue);
                    if (moderationReason != null)
                    {
                        _logger.LogInformation("[AGENT] Session #{SessionId} generated filtered code: {Reason}. Sending to moderation.", 
                            session.Id, moderationReason);
                        
                        // Add to code moderation queue instead of direct execution
                        _codeModerationService.AddPendingCode(
                            session.UserPrompt,
                            codeToQueue,
                            undoToQueue,
                            moderationReason,
                            session.Source,
                            session.Author,
                            session.UserId,
                            null); // No command ID yet - will be set when approved
                        
                        // Create placeholder command with PendingModeration status
                        var placeholderEntry = _commandQueue.AddCommandWithStatus(
                            session.UserPrompt,
                            codeToQueue,
                            undoToQueue,
                            session.Source,
                            session.Author,
                            null,
                            session.UserId,
                            $"⚠️ Code contains filtered content: {moderationReason}. Waiting for moderator approval...",
                            CommandStatus.PendingModeration,
                            false); // Don't queue yet
                        
                        session.PendingCommandId = placeholderEntry.Id;
                        CompleteSession(session, true); // Session succeeded but needs moderation
                        return;
                    }
                }
                
                // For AgenticTest mode, queue to test client first
                if (session.Mode == AgentMode.AgenticTest && IsTestClientEnabled)
                {
                    QueueForTesting(-session.Id, codeToQueue, session.UserPrompt);
                    session.PendingCommandId = -session.Id;
                    session.CurrentPhase = AgentPhase.Testing;
                    _logger.LogInformation("[AGENT] Session #{SessionId} sent final code to test client", session.Id);
                }
                else
                {
                    var entry = _commandQueue.AddCommand(
                        session.UserPrompt,
                        codeToQueue,
                        undoToQueue,
                        session.Source,
                        session.Author,
                        null,
                        session.UserId);
                    
                    session.PendingCommandId = entry.Id;
                    session.PendingCode = codeToQueue;
                    session.CurrentPhase = AgentPhase.Testing;
                    
                    _logger.LogInformation("[AGENT] Session #{SessionId} queued final code as command #{CommandId}", 
                        session.Id, entry.Id);
                }
            }
            else
            {
                var codeToExecute = WrapCodeForDataCapture(response.Code ?? "");
                var cmdId = -(session.Id * 1000 + session.CurrentIteration);
                
                // For AgenticTest mode, preparation code also goes to test client
                if (session.Mode == AgentMode.AgenticTest && IsTestClientEnabled)
                {
                    QueueForTesting(cmdId, codeToExecute, session.UserPrompt);
                }
                else
                {
                    _commandQueue.QueueInteractiveCode(cmdId, codeToExecute);
                }
                
                session.PendingCommandId = cmdId;
                session.PendingCode = codeToExecute;
                
                session.CurrentPhase = response.Phase?.ToLower() switch
                {
                    "prepare" => AgentPhase.Preparing,
                    "fix" => AgentPhase.Fixing,
                    _ => AgentPhase.Generating
                };
                
                _logger.LogInformation("[AGENT] Session #{SessionId} iteration {Iteration} - Phase: {Phase}", 
                    session.Id, session.CurrentIteration, session.CurrentPhase);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AGENT] Error processing iteration for session #{SessionId}", session.Id);
            CompleteSession(session, false);
        }
    }
    
    private void CompleteSession(AgentSession session, bool wasSuccessful)
    {
        session.IsComplete = true;
        session.WasSuccessful = wasSuccessful;
        session.CompletedAt = DateTime.UtcNow;
        session.CurrentPhase = wasSuccessful ? AgentPhase.Complete : AgentPhase.Failed;
        
        _logger.LogInformation("[AGENT] Session #{SessionId} completed - Success: {Success}", session.Id, wasSuccessful);
    }
    
    #endregion
    
    #region AI Helpers
    
    private async Task<AgentAiResponse?> CallAgentAiAsync(AgentSession session, string? previousResult, string? error)
    {
        var userContent = new StringBuilder();
        userContent.AppendLine($"Request: {session.UserPrompt}");
        userContent.AppendLine($"Iteration: {session.CurrentIteration}/{session.MaxIterations}");
        userContent.AppendLine($"Current Phase: {session.CurrentPhase}");
        userContent.AppendLine($"Mode: {session.Mode}");
        
        if (!string.IsNullOrEmpty(previousResult))
        {
            userContent.AppendLine($"\n[PREVIOUS RESULT]:\n{previousResult}");
        }
        
        if (!string.IsNullOrEmpty(error))
        {
            userContent.AppendLine($"\n[ERROR FROM LAST EXECUTION]:\n{error}");
            userContent.AppendLine("\nPlease fix the code based on this error.");
        }
        
        if (session.Steps.Any())
        {
            userContent.AppendLine("\n[PREVIOUS STEPS]:");
            foreach (var step in session.Steps.TakeLast(3))
            {
                userContent.AppendLine($"- Step {step.StepNumber} ({step.Phase}): {(step.Success == true ? "Success" : step.Success == false ? $"Failed: {step.Error}" : "Pending")}");

                if (!string.IsNullOrEmpty(step.ResultData))
                {
                    var truncated = step.ResultData.Length > 500 ? step.ResultData[..500] + "..." : step.ResultData;
                    userContent.AppendLine($"  Data: {truncated}");
                }
            }
        }
        
        var systemMessage = new ChatMessage { Role = "system", Content = AgenticSystemPrompt };
        var userMessage = new ChatMessage { Role = "user", Content = userContent.ToString() };
        
        var messages = new List<ChatMessage> { systemMessage, userMessage };
        
        // Track chat history for debugging
        session.ChatHistory.Add(systemMessage);
        session.ChatHistory.Add(userMessage);
        
        var response = await _openRouterService.ChatCompletionAsync(messages);
        
        if (string.IsNullOrEmpty(response))
        {
            _logger.LogError("[AGENT] OpenRouterService returned empty response for session #{SessionId}", session.Id);
            return null;
        }
        
        // Add assistant response to chat history
        session.ChatHistory.Add(new ChatMessage { Role = "assistant", Content = response });
        
        return ParseAgentAiResponse(response);
    }
    
    private async Task<string?> AskAiToFixCodeAsync(string currentCode, string error, string originalPrompt)
    {
        if (!_openRouterService.IsConfigured)
        {
            _logger.LogWarning("[AGENT] Cannot fix code - API key not configured");
            return null;
        }
        
        var userContent = new StringBuilder();
        userContent.AppendLine("The following Lua code failed to execute on a Garry's Mod server.");
        userContent.AppendLine($"\n**Original Request:**\n{originalPrompt}");
        userContent.AppendLine($"\n**Code that failed:**\n```lua\n{currentCode}\n```");
        userContent.AppendLine($"\n**Error message:**\n{error}");
        userContent.AppendLine("\nPlease fix the code. Return ONLY the fixed code.");
        
        var messages = new List<ChatMessage>
        {
            new() { Role = "system", Content = ErrorFixSystemPrompt },
            new() { Role = "user", Content = userContent.ToString() }
        };
        
        try
        {
            var response = await _openRouterService.ChatCompletionAsync(messages);
            
            if (string.IsNullOrEmpty(response))
            {
                return null;
            }
            
            return OpenRouterService.CleanLuaCode(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AGENT] Failed to get AI fix for code");
            return null;
        }
    }
    
    private AgentAiResponse? ParseAgentAiResponse(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            _logger.LogWarning("[AGENT] AI returned empty content");
            return null;
        }
        
        try
        {
            var jsonContent = content.Trim();
            
            // Extract JSON from markdown code blocks
            if (jsonContent.Contains("```json"))
            {
                var start = jsonContent.IndexOf("```json") + 7;
                var end = jsonContent.IndexOf("```", start);
                if (end > start)
                {
                    jsonContent = jsonContent[start..end].Trim();
                }
            }
            else if (jsonContent.Contains("```"))
            {
                var start = jsonContent.IndexOf("```") + 3;
                var end = jsonContent.IndexOf("```", start);
                if (end > start)
                {
                    jsonContent = jsonContent[start..end].Trim();
                }
            }
            
            // Try to find JSON object in the content
            if (!jsonContent.TrimStart().StartsWith("{"))
            {
                var jsonStart = jsonContent.IndexOf('{');
                if (jsonStart >= 0)
                {
                    jsonContent = jsonContent[jsonStart..];
                    var jsonEnd = jsonContent.LastIndexOf('}');
                    if (jsonEnd >= 0)
                    {
                        jsonContent = jsonContent[..(jsonEnd + 1)];
                    }
                }
            }
            
            // Validate that we have JSON-like content
            if (!jsonContent.TrimStart().StartsWith("{") || !jsonContent.TrimEnd().EndsWith("}"))
            {
                _logger.LogWarning("[AGENT] AI response doesn't contain valid JSON. Content: {Content}", 
                    content.Length > 200 ? content[..200] + "..." : content);
                
                // Fallback: treat the entire response as code
                return new AgentAiResponse
                {
                    Phase = "generate",
                    Thinking = "Direct code generation (fallback)",
                    Code = ExtractLuaCode(content),
                    IsComplete = true
                };
            }
            
            var response = JsonSerializer.Deserialize<AgentAiResponse>(jsonContent, new JsonSerializerOptions 
            { 
                PropertyNameCaseInsensitive = true,
                AllowTrailingCommas = true
            });
            
            if (response == null)
            {
                _logger.LogWarning("[AGENT] Failed to deserialize AI response");
                return new AgentAiResponse
                {
                    Phase = "generate",
                    Thinking = "Direct code generation (deserialization failed)",
                    Code = ExtractLuaCode(content),
                    IsComplete = true
                };
            }
            
            // Validate the response has required fields
            if (string.IsNullOrEmpty(response.Code) && !response.IsComplete)
            {
                _logger.LogWarning("[AGENT] AI response missing code field");
                response.Code = ExtractLuaCode(content);
                if (string.IsNullOrEmpty(response.Code))
                {
                    response.IsComplete = true; // Mark as complete if we can't extract code
                }
            }
            
            return response;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "[AGENT] JSON parsing error. Content: {Content}", 
                content.Length > 200 ? content[..200] + "..." : content);
            
            // Fallback: extract any Lua code we can find
            return new AgentAiResponse
            {
                Phase = "generate",
                Thinking = "Direct code generation (JSON parse error)",
                Code = ExtractLuaCode(content),
                IsComplete = true
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AGENT] Error parsing AI response");
            return null;
        }
    }
    
    /// <summary>
    /// Extracts Lua code from AI response, removing markdown formatting.
    /// </summary>
    private string ExtractLuaCode(string content)
    {
        var code = content;
        
        // Remove ```lua``` blocks
        if (code.Contains("```lua"))
        {
            var start = code.IndexOf("```lua") + 6;
            var end = code.IndexOf("```", start);
            if (end > start)
            {
                code = code[start..end].Trim();
            }
        }
        else if (code.Contains("```"))
        {
            // Generic code block
            var start = code.IndexOf("```") + 3;
            var end = code.IndexOf("```", start);
            if (end > start)
            {
                code = code[start..end].Trim();
            }
        }
        
        return code.Trim();
    }
    
    private string WrapCodeForDataCapture(string code)
    {
        // Ensure code is properly trimmed and has newlines to avoid syntax errors like "endend)"
        var trimmedCode = code.Trim();
        
        // Use regular string concatenation since Lua uses {} which conflicts with C# interpolation
        return @"
            local _capturedOutput = {}
            local _originalPrint = print
            local _originalPrintTable = PrintTable
            
            print = function(...)
                local args = {...}
                local str = """"
                for i, v in ipairs(args) do
                    str = str .. tostring(v) .. (i < #args and ""\t"" or """")
                end
                table.insert(_capturedOutput, str)
                _originalPrint(...)
            end
            
            PrintTable = function(tbl, indent, done)
                indent = indent or 0
                done = done or {}
                local prefix = string.rep(""  "", indent)
                
                if type(tbl) == ""table"" then
                    if done[tbl] then
                        table.insert(_capturedOutput, prefix .. ""(circular reference)"")
                        return
                    end
                    done[tbl] = true
                    
                    for k, v in pairs(tbl) do
                        local keyStr = tostring(k)
                        if type(v) == ""table"" then
                            table.insert(_capturedOutput, prefix .. keyStr .. "" = {"")
                            PrintTable(v, indent + 1, done)
                            table.insert(_capturedOutput, prefix .. ""}"")
                        else
                            table.insert(_capturedOutput, prefix .. keyStr .. "" = "" .. tostring(v))
                        end
                    end
                else
                    table.insert(_capturedOutput, prefix .. tostring(tbl))
                end
                
                if indent == 0 then _originalPrintTable(tbl) end
            end
            
            local _success, _err = pcall(function()
                " + trimmedCode + @"
            end)
            
            print = _originalPrint
            PrintTable = _originalPrintTable
            
            _AI_CAPTURED_DATA = table.concat(_capturedOutput, ""\n"")
            if not _success then 
                -- Ensure error message is a proper string, not just 'false'
                local errMsg = _err
                if errMsg == nil or errMsg == false then
                    errMsg = ""Unknown error occurred during execution""
                else
                    errMsg = tostring(errMsg)
                end
                error(errMsg) 
            end
            ";
    }
    
    #endregion
    
    #region Cleanup
    
    public void CleanupOldSessions(TimeSpan maxAge)
    {
        lock (_lock)
        {
            var cutoff = DateTime.UtcNow - maxAge;
            var toRemove = _sessions.Values
                .Where(s => s.IsComplete && s.CompletedAt < cutoff)
                .Select(s => s.Id)
                .ToList();
            
            foreach (var id in toRemove)
            {
                _sessions.Remove(id);
            }
        }
    }
    
    #endregion
    
    #region Inner Classes
    
    private class TestQueueItem
    {
        public int CommandId { get; set; }
        public string OriginalPrompt { get; set; } = "";
        public string CurrentCode { get; set; } = "";
        public bool CleanupAfterTest { get; set; }
        public int AttemptCount { get; set; }
    }
    
    private class PendingExecution
    {
        public int CommandId { get; set; }
        public string OriginalPrompt { get; set; } = "";
        public string CurrentCode { get; set; } = "";
        public DateTime StartedAt { get; set; }
        public int TimeoutSeconds { get; set; }
        public int AttemptCount { get; set; }
        public bool CleanupAfterTest { get; set; }
        public bool IsTestClient { get; set; }
    }
    
    #endregion
}

#region Models

/// <summary>
/// Request to create an agent session.
/// </summary>
public class AgentSessionRequest
{
    public string Prompt { get; set; } = "";
    public string? Source { get; set; }
    public string? Author { get; set; }
    public string? UserId { get; set; }
    public int MaxIterations { get; set; } = 5;
    public bool UseTestClient { get; set; } = false;
}

/// <summary>
/// An agentic session for game interaction.
/// </summary>
public class AgentSession
{
    public int Id { get; set; }
    public string UserPrompt { get; set; } = "";
    public string Source { get; set; } = "web";
    public string Author { get; set; } = "anonymous";
    public string? UserId { get; set; }
    public AgentMode Mode { get; set; } = AgentMode.Interactive;
    public int MaxIterations { get; set; } = 5;
    public int CurrentIteration { get; set; } = 0;
    public AgentPhase CurrentPhase { get; set; } = AgentPhase.Preparing;
    public bool IsComplete { get; set; } = false;
    public bool WasSuccessful { get; set; } = false;
    public string? FinalExecutionCode { get; set; }
    public string? FinalUndoCode { get; set; }
    public List<AgentStep> Steps { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public int? PendingCommandId { get; set; }
    public string? PendingCode { get; set; }
    
    /// <summary>
    /// Full chat message history for debugging (shows actual API messages).
    /// </summary>
    public List<ChatMessage> ChatHistory { get; set; } = new();
}

/// <summary>
/// A step in an agent session.
/// </summary>
public class AgentStep
{
    public int StepNumber { get; set; }
    public string Phase { get; set; } = "";
    public string? Code { get; set; }
    public bool? Success { get; set; }
    public string? Error { get; set; }
    public string? ResultData { get; set; }
    public string? AiThinking { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Agent execution mode.
/// </summary>
public enum AgentMode
{
    Direct,         // Code goes straight to main client
    Interactive,    // AI can iterate on main client
    TestClient,     // Code is tested before main client
    AgenticTest     // Full AI iteration on test client
}

/// <summary>
/// Agent session phases.
/// </summary>
public enum AgentPhase
{
    Preparing,
    Generating,
    Testing,
    Fixing,
    Complete,
    Failed
}

/// <summary>
/// AI response for agent sessions.
/// </summary>
public class AgentAiResponse
{
    public string? Phase { get; set; }
    public string? Thinking { get; set; }
    public string? Code { get; set; }
    public string? UndoCode { get; set; }
    public bool IsComplete { get; set; }
    public string? SearchQuery { get; set; }
}

#endregion
