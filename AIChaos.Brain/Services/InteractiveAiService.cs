using System.Text;
using System.Text.Json;
using AIChaos.Brain.Models;

namespace AIChaos.Brain.Services;

/// <summary>
/// Service for managing interactive AI sessions that can iterate with the game.
/// </summary>
public class InteractiveAiService
{
    private readonly HttpClient _httpClient;
    private readonly SettingsService _settingsService;
    private readonly CommandQueueService _commandQueue;
    private readonly ILogger<InteractiveAiService> _logger;
    
    private readonly Dictionary<int, InteractiveSession> _sessions = new();
    private readonly object _lock = new();
    private int _nextSessionId = 1;
    
    private const string InteractiveSystemPrompt = """
        You are an expert Lua scripter for Garry's Mod (GLua) with the ability to interact with the game iteratively.
        You will receive a request from a livestream chat and can execute preparation code to gather information before generating your final code.
        
        **INTERACTION PHASES:**
        1. **PREPARE** - Run code to gather information (search models, check entities, get player state)
        2. **GENERATE** - Generate the main execution code based on gathered info
        3. **FIX** - If execution fails, analyze errors and fix the code
        
        **RESPONSE FORMAT:**
        You must respond with a JSON object in this exact format:
        ```json
        {
            "phase": "prepare|generate|fix",
            "thinking": "Your reasoning about what to do",
            "code": "The Lua code to execute",
            "undoCode": "Undo code (only for 'generate' phase)",
            "isComplete": false,
            "searchQuery": "optional: what you're searching for"
        }
        ```
        
        **PREPARATION CODE EXAMPLES:**
        - Search for models: `local models = {} for _, ent in pairs(ents.GetAll()) do local m = ent:GetModel() if m and m:find("pattern") then table.insert(models, m) end end PrintTable(models)`
        - Find NPCs: `for _, npc in pairs(ents.FindByClass("npc_*")) do print(npc:GetClass(), npc:GetPos()) end`
        - Check player state: `local p = Entity(1) print("Health:", p:Health(), "Pos:", p:GetPos(), "Weapon:", p:GetActiveWeapon():GetClass())`
        - List available models in a category: Search patterns like "models/props_", "models/player/", etc.
        
        **IMPORTANT RULES:**
        1. Preparation code should use print() or PrintTable() to output data that will be returned to you
        2. Keep preparation code focused and minimal
        3. After getting preparation results, generate the main code with full context
        4. If the main code fails, analyze the error and generate fixed code
        5. Maximum iterations are limited - be efficient
        
        **EXECUTION ENVIRONMENT:**
        - You are executing in a SERVER environment
        - For client-side effects (UI, HUD, Screen Effects), use `RunOnClient([[ ... ]])`
        - `LocalPlayer()` is only valid inside `RunOnClient` wrapper
        - On server, use `player.GetAll()` or `Entity(1)` for players
        
        **SAFETY RULES:**
        - Do not use 'os.execute', 'http.Fetch' (outbound), or file system writes
        - Do not crash the server
        - NEVER use 'entity:Remove()' on key story objects
        - For model swaps, use bonemerge and temporarily hide originals
        """;
    
    public InteractiveAiService(
        HttpClient httpClient,
        SettingsService settingsService,
        CommandQueueService commandQueue,
        ILogger<InteractiveAiService> logger)
    {
        _httpClient = httpClient;
        _settingsService = settingsService;
        _commandQueue = commandQueue;
        _logger = logger;
    }
    
    /// <summary>
    /// Creates a new interactive session.
    /// </summary>
    public async Task<InteractiveSession> CreateSessionAsync(InteractiveTriggerRequest request)
    {
        var session = new InteractiveSession
        {
            Id = GetNextSessionId(),
            UserPrompt = request.Prompt,
            Source = request.Source ?? "web",
            Author = request.Author ?? "anonymous",
            UserId = request.UserId,
            MaxIterations = request.MaxIterations > 0 ? request.MaxIterations : 5
        };
        
        lock (_lock)
        {
            _sessions[session.Id] = session;
        }
        
        _logger.LogInformation("[INTERACTIVE] Created session #{SessionId} for: {Prompt}", session.Id, request.Prompt);
        
        // Start the first iteration
        await ProcessIterationAsync(session);
        
        return session;
    }
    
    /// <summary>
    /// Gets a session by ID.
    /// </summary>
    public InteractiveSession? GetSession(int sessionId)
    {
        lock (_lock)
        {
            return _sessions.TryGetValue(sessionId, out var session) ? session : null;
        }
    }
    
    /// <summary>
    /// Reports execution result for an interactive session command.
    /// </summary>
    public async Task<bool> ReportResultAsync(int commandId, bool success, string? error, string? resultData)
    {
        InteractiveSession? session = null;
        
        lock (_lock)
        {
            session = _sessions.Values.FirstOrDefault(s => s.PendingCommandId == commandId);
        }
        
        if (session == null) return false;
        
        // Update the last step with results
        var lastStep = session.Steps.LastOrDefault();
        if (lastStep != null)
        {
            lastStep.Success = success;
            lastStep.Error = error;
            lastStep.ResultData = resultData;
        }
        
        session.PendingCommandId = null;
        session.PendingCode = null;
        
        _logger.LogInformation("[INTERACTIVE] Session #{SessionId} received result - Success: {Success}, Error: {Error}", 
            session.Id, success, error ?? "none");
        
        if (success)
        {
            // Process next iteration based on phase
            if (session.CurrentPhase == InteractivePhase.Preparing)
            {
                // Continue to generate phase with the gathered data
                await ProcessIterationAsync(session, resultData);
            }
            else if (session.CurrentPhase == InteractivePhase.Generating || 
                     session.CurrentPhase == InteractivePhase.Fixing ||
                     session.CurrentPhase == InteractivePhase.Testing)
            {
                // Success! Mark as complete
                CompleteSession(session, true);
            }
        }
        else
        {
            // Error occurred - try to fix it
            if (session.CurrentIteration < session.MaxIterations)
            {
                session.CurrentPhase = InteractivePhase.Fixing;
                await ProcessIterationAsync(session, error: error);
            }
            else
            {
                // Max iterations reached
                CompleteSession(session, false);
            }
        }
        
        return true;
    }
    
    /// <summary>
    /// Processes an iteration of the interactive session.
    /// </summary>
    private async Task ProcessIterationAsync(InteractiveSession session, string? previousResult = null, string? error = null)
    {
        if (session.IsComplete || session.CurrentIteration >= session.MaxIterations)
        {
            CompleteSession(session, session.WasSuccessful);
            return;
        }
        
        session.CurrentIteration++;
        
        try
        {
            var response = await CallAiAsync(session, previousResult, error);
            
            if (response == null)
            {
                _logger.LogError("[INTERACTIVE] AI response was null for session #{SessionId}", session.Id);
                CompleteSession(session, false);
                return;
            }
            
            // Add step
            var step = new InteractionStep
            {
                StepNumber = session.CurrentIteration,
                Phase = response.Phase ?? "unknown",
                Code = response.Code,
                AiThinking = response.Thinking
            };
            session.Steps.Add(step);
            
            // Check if AI says we're done
            if (response.IsComplete)
            {
                session.FinalExecutionCode = response.Code;
                session.FinalUndoCode = response.UndoCode;
                
                // Queue the final code for execution
                var entry = _commandQueue.AddCommand(
                    session.UserPrompt,
                    response.Code ?? "",
                    response.UndoCode ?? "print(\"Undo not available\")",
                    session.Source,
                    session.Author,
                    null,
                    session.UserId);
                
                session.PendingCommandId = entry.Id;
                session.PendingCode = response.Code;
                session.CurrentPhase = InteractivePhase.Testing;
                
                _logger.LogInformation("[INTERACTIVE] Session #{SessionId} queued final code as command #{CommandId}", 
                    session.Id, entry.Id);
            }
            else
            {
                // Queue preparation/fix code
                var codeToExecute = WrapCodeForDataCapture(response.Code ?? "");
                
                lock (_commandQueue)
                {
                    // Use a special command ID for interactive sessions
                    var cmdId = -(session.Id * 1000 + session.CurrentIteration);
                    _commandQueue.QueueInteractiveCode(cmdId, codeToExecute);
                    session.PendingCommandId = cmdId;
                    session.PendingCode = codeToExecute;
                }
                
                session.CurrentPhase = response.Phase?.ToLower() switch
                {
                    "prepare" => InteractivePhase.Preparing,
                    "fix" => InteractivePhase.Fixing,
                    _ => InteractivePhase.Generating
                };
                
                _logger.LogInformation("[INTERACTIVE] Session #{SessionId} iteration {Iteration} - Phase: {Phase}", 
                    session.Id, session.CurrentIteration, session.CurrentPhase);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[INTERACTIVE] Error processing iteration for session #{SessionId}", session.Id);
            CompleteSession(session, false);
        }
    }
    
    /// <summary>
    /// Calls the AI with the current session context.
    /// </summary>
    private async Task<AiInteractiveResponse?> CallAiAsync(InteractiveSession session, string? previousResult, string? error)
    {
        var userContent = new StringBuilder();
        userContent.AppendLine($"Request: {session.UserPrompt}");
        userContent.AppendLine($"Iteration: {session.CurrentIteration}/{session.MaxIterations}");
        userContent.AppendLine($"Current Phase: {session.CurrentPhase}");
        
        if (!string.IsNullOrEmpty(previousResult))
        {
            userContent.AppendLine($"\n[PREVIOUS RESULT]:\n{previousResult}");
        }
        
        if (!string.IsNullOrEmpty(error))
        {
            userContent.AppendLine($"\n[ERROR FROM LAST EXECUTION]:\n{error}");
            userContent.AppendLine("\nPlease fix the code based on this error.");
        }
        
        // Add conversation history
        if (session.Steps.Any())
        {
            userContent.AppendLine("\n[PREVIOUS STEPS]:");
            foreach (var step in session.Steps.TakeLast(3))
            {
                userContent.AppendLine($"- Step {step.StepNumber} ({step.Phase}): {(step.Success == true ? "Success" : step.Success == false ? $"Failed: {step.Error}" : "Pending")}");
                if (!string.IsNullOrEmpty(step.ResultData) && step.ResultData.Length > 0)
                {
                    var truncatedData = step.ResultData.Length > 500 
                        ? step.ResultData.Substring(0, 500) + "..." 
                        : step.ResultData;
                    userContent.AppendLine($"  Data: {truncatedData}");
                }
            }
        }
        
        var settings = _settingsService.Settings;
        var requestBody = new
        {
            model = settings.OpenRouter.Model,
            messages = new[]
            {
                new { role = "system", content = InteractiveSystemPrompt },
                new { role = "user", content = userContent.ToString() }
            }
        };
        
        var request = new HttpRequestMessage(HttpMethod.Post, $"{settings.OpenRouter.BaseUrl}/chat/completions")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(requestBody),
                Encoding.UTF8,
                "application/json")
        };
        
        request.Headers.Add("Authorization", $"Bearer {settings.OpenRouter.ApiKey}");
        
        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        
        var responseContent = await response.Content.ReadAsStringAsync();
        var jsonDoc = JsonDocument.Parse(responseContent);
        
        var aiContent = jsonDoc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? "";
        
        // Parse the JSON response from AI
        return ParseAiResponse(aiContent);
    }
    
    /// <summary>
    /// Parses the AI's JSON response.
    /// </summary>
    private AiInteractiveResponse? ParseAiResponse(string content)
    {
        try
        {
            // Extract JSON from markdown code blocks if present
            var jsonContent = content;
            if (content.Contains("```json"))
            {
                var start = content.IndexOf("```json") + 7;
                var end = content.IndexOf("```", start);
                if (end > start)
                {
                    jsonContent = content.Substring(start, end - start).Trim();
                }
            }
            else if (content.Contains("```"))
            {
                var start = content.IndexOf("```") + 3;
                var end = content.IndexOf("```", start);
                if (end > start)
                {
                    jsonContent = content.Substring(start, end - start).Trim();
                }
            }
            
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            
            return JsonSerializer.Deserialize<AiInteractiveResponse>(jsonContent, options);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse AI response as JSON, treating as raw code");
            
            // Fallback: treat entire content as code for backward compatibility
            return new AiInteractiveResponse
            {
                Phase = "generate",
                Thinking = "Direct code generation",
                Code = content.Replace("```lua", "").Replace("```", "").Trim(),
                IsComplete = true
            };
        }
    }
    
    /// <summary>
    /// Wraps code to capture print output and return it.
    /// </summary>
    private string WrapCodeForDataCapture(string code)
    {
        // Wrap the code to capture output and return it to the report endpoint
        return """
            local _capturedOutput = {}
            local _originalPrint = print
            print = function(...)
                local args = {...}
                local str = ""
                for i, v in ipairs(args) do
                    str = str .. tostring(v) .. (i < #args and "\t" or "")
                end
                table.insert(_capturedOutput, str)
                _originalPrint(...)
            end
            
            local _success, _err = pcall(function()
            """ + code + """
            end)
            
            print = _originalPrint
            
            -- Return captured data
            _AI_CAPTURED_DATA = table.concat(_capturedOutput, "\n")
            if not _success then
                error(_err)
            end
            """;
    }
    
    /// <summary>
    /// Marks a session as complete.
    /// </summary>
    private void CompleteSession(InteractiveSession session, bool wasSuccessful)
    {
        session.IsComplete = true;
        session.WasSuccessful = wasSuccessful;
        session.CompletedAt = DateTime.UtcNow;
        session.CurrentPhase = wasSuccessful ? InteractivePhase.Complete : InteractivePhase.Failed;
        
        _logger.LogInformation("[INTERACTIVE] Session #{SessionId} completed - Success: {Success}, Iterations: {Iterations}", 
            session.Id, wasSuccessful, session.CurrentIteration);
    }
    
    private int GetNextSessionId()
    {
        lock (_lock)
        {
            return _nextSessionId++;
        }
    }
    
    /// <summary>
    /// Gets all active sessions.
    /// </summary>
    public List<InteractiveSession> GetActiveSessions()
    {
        lock (_lock)
        {
            return _sessions.Values.Where(s => !s.IsComplete).ToList();
        }
    }
    
    /// <summary>
    /// Cleans up old completed sessions.
    /// </summary>
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
}

/// <summary>
/// Response structure from AI for interactive mode.
/// </summary>
public class AiInteractiveResponse
{
    public string? Phase { get; set; }
    public string? Thinking { get; set; }
    public string? Code { get; set; }
    public string? UndoCode { get; set; }
    public bool IsComplete { get; set; }
    public string? SearchQuery { get; set; }
}
