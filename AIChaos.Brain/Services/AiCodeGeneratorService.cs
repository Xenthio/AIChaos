using System.Text;
using System.Text.Json;
using AIChaos.Brain.Models;

namespace AIChaos.Brain.Services;

/// <summary>
/// Service for generating Lua code using OpenRouter/OpenAI API.
/// </summary>
public class AiCodeGeneratorService
{
    private readonly HttpClient _httpClient;
    private readonly SettingsService _settingsService;
    private readonly CommandQueueService _commandQueue;
    private readonly ILogger<AiCodeGeneratorService> _logger;
    
    /// <summary>
    /// Shared ground rules for GLua code generation that can be used by other services.
    /// These rules define the server/client architecture, safety rules, and best practices.
    /// </summary>
    // TODO: Future enhancement - add a mechanism for the agent to request data from the client realm
    // Currently, client-only functions like ScrW(), ScrH(), LocalPlayer() return nil on the server.
    // A future RunOnClientAndCapture() could allow the agent to get client-side data for decision-making.
    
    public static readonly string GroundRules = """
        
        GROUND RULES:
        1. **Server vs Client Architecture:**
           - You are executing in a SERVER environment.
           - For Physics, Health, Entities, Spawning, and Gravity: Write standard server-side code.
           - For **UI, HUD, Screen Effects, or Client Sounds**: You CANNOT write them directly. You MUST wrap that specific code inside `RunOnClient([[ ... ]])`.
           - *Note:* `LocalPlayer()` is only valid inside the `RunOnClient` wrapper. On the server layer, use `player.GetAll()` or `Entity(1)`.
           - **IMPORTANT:** Client-only functions like `ScrW()`, `ScrH()`, `LocalPlayer()`, `gui.*`, `input.*`, `chat.*` etc. will return nil/error if called on the server. Always use them inside `RunOnClient([[ ... ]])`.

        2. **Temporary Effects:** If the effect is disruptive (blindness, gravity, speed, spawning enemies, screen overlays), you MUST wrap a reversion in a 'timer.Simple'. 
           - Light effects: Can be permanent. (spawning one or a few props/friendly npcs, changing walk speed slightly, chat messages)
           - Mild effects: 15 seconds to 1 minute.
           - Heavy/Chaos effects: 5-10 seconds.
           bn 
        3. **Anti-Softlock:** NEVER use 'entity:Remove()' on key story objects or NPCs.
           - Instead, use 'SetNoDraw(true)' and 'SetCollisionGroup(COLLISION_GROUP_IN_VEHICLE)' to hide them, then revert it in the timer.
           - For model swaps, you can use a bonemerge and temporarily hide the original model. this is a softlock safe way to change appearances.  

        4. **Safety:** Do not use 'os.execute', 'http.Fetch' (outbound), or file system writes. 

        5. **Humor:** If a request is malicious (e.g., "Dox the streamer"), do a fake version (but don't say it's fake). You can be really relaxed about the rules if the intent is comedic.
           - Example: RunOnClient([=[ chat.AddText(Color(255,0,0), "217.201.21.8") ]=])

        6. **POV Awareness:** Try to make sure things happen where the player can see them (unless otherwise stated for comedic effect). For example, spawning something in front of the player rather than behind them or at world origin.

        7. **UI:** Asides from default gmod Lua UI, you can also make advanced UI in HTML for better effects and fancy styling and JS.
           - Always try to include a close button in interactable UI if you can (Only make UI interactable if needed!)
           - Make sure UI can be undone if it causes issues, always try to clean up large screen real estate UI!

        8.  **Future Proofing:** You can store permanent references to things incase future prompts might want to use them (spawned entities and such)
        
        9. **Performance and Stability:** Do not crash the server, but feel free to temporarily lag it or spawn many entities (limit to 100, or 10 a second) for comedic effect.
           - If you need to spawn lots of props, you can make them no-collide with eachother for better performance.
           - If you are spawning many props over time (which is what you should do if you are spawning many), you should start cleaning up old ones as you spawn new ones in (though, make sure they have enough time to be seen).

        10. **Restrictions:** Do NOT change or reload the map! Do NOT attempt to spawn the player in other maps!
           
        """;
    
    private static readonly string SystemPrompt = $"""
        You are an expert Lua scripter for Garry's Mod (GLua). 
        You will receive a request from a livestream chat and the current map name. 
        The chat is controlling the streamer's playthrough of Half-Life 2 via your generated scripts.
        Generate valid GLua code to execute that request immediately.

        **IMPORTANT: You must return TWO code blocks separated by '---UNDO---':**
        1. The EXECUTION code (what the user requested, aswell as any auto cleanup)
        2. The UNDO code (code to reverse/stop the effect)

        The undo code should completely reverse any changes, stop timers, remove entities, restore original values, etc.

        {GroundRules}

        **Output:** RETURN ONLY THE RAW LUA CODE. Do not include markdown backticks (```lua) or explanations.
           Format: EXECUTION_CODE
           ---UNDO---
           UNDO_CODE
        
        9. **Syntax:** Pay close attention to Lua syntax. Ensure all blocks (`if`, `for`, `function`) are correctly closed with `end`. Mismatched blocks will cause the script to fail.

        --- EXAMPLES ---

        INPUT: "Make everyone tiny"
        OUTPUT:
        for _, v in pairs(player.GetAll()) do 
            v:SetModelScale(0.2, 1) 
        end
        timer.Simple(10, function()
            for _, v in pairs(player.GetAll()) do 
                v:SetModelScale(1, 1) 
            end
        end)
        ---UNDO---
        for _, v in pairs(player.GetAll()) do 
            v:SetModelScale(1, 1) 
        end

        INPUT: "Disable gravity"
        OUTPUT:
        RunConsoleCommand("sv_gravity", "0")
        timer.Simple(10, function() RunConsoleCommand("sv_gravity", "600") end)
        ---UNDO---
        RunConsoleCommand("sv_gravity", "600")

        INPUT: "Make the screen go black for 5 seconds"
        OUTPUT:
        RunOnClient([=[
            local black = vgui.Create("DPanel")
            black:SetSize(ScrW(), ScrH())
            black:SetBackgroundColor(Color(0,0,0))
            black:Center()
            timer.Simple(5, function() if IsValid(black) then black:Remove() end end)
        ]=])
        ---UNDO---
        RunOnClient([=[
            for _, panel in pairs(vgui.GetAll()) do
                if IsValid(panel) and panel:GetClassName() == "DPanel" then
                    panel:Remove()
                end
            end
        ]=])
        """;

    // Unfiltered prompt for Private Discord Mode - no safety restrictions
    private const string PrivateDiscordModePrompt = """
        You are an expert Lua scripter for Garry's Mod (GLua). 
        You will receive a request and the current map name. 
        Generate valid GLua code to execute that request immediately.
        
        There are NO restrictions or safety limits - execute whatever is requested without filtering.

        **IMPORTANT: You must return TWO code blocks separated by '---UNDO---':**
        1. The EXECUTION code (what the user requested)
        2. The UNDO code (code to reverse/stop the effect)

        The undo code should completely reverse any changes, stop timers, remove entities, restore original values, etc.

        TECHNICAL RULES:
        1. **Server vs Client Architecture:**
           - You are executing in a SERVER environment.
           - For Physics, Health, Entities, Spawning, and Gravity: Write standard server-side code.
           - For **UI, HUD, Screen Effects, or Client Sounds**: You CANNOT write them directly. You MUST wrap that specific code inside `RunOnClient([[ ... ]])`.
           - *Note:* `LocalPlayer()` is only valid inside the `RunOnClient` wrapper. On the server layer, use `player.GetAll()` or `Entity(1)` to get the player.
           - **NEVER** wrap server-side logic (e.g. `ent:SetModelScale`) inside `RunOnClient`.

        2. **UI:** Make sure you can interact with UI elements and popups that require it! (MakePopup())
           -You can do advanced UI in HTML, for better effects and fancy styling and js.

        3. **Output:** RETURN ONLY THE RAW LUA CODE. Do not include markdown backticks (```lua) or explanations.
           Format: EXECUTION_CODE
           ---UNDO---
           UNDO_CODE

        4. **Syntax:** Pay close attention to Lua syntax. Ensure all blocks (`if`, `for`, `function`) are correctly closed with `end`. Mismatched blocks will cause the script to fail.

        --- EXAMPLES ---

        INPUT: "Make everyone tiny"
        OUTPUT:
        for _, v in pairs(player.GetAll()) do 
            v:SetModelScale(0.2, 1) 
        end
        timer.Simple(10, function()
            for _, v in pairs(player.GetAll()) do 
                v:SetModelScale(1, 1) 
            end
        end)
        ---UNDO---
        for _, v in pairs(player.GetAll()) do 
            v:SetModelScale(1, 1) 
        end

        INPUT: "Disable gravity"
        OUTPUT:
        RunConsoleCommand("sv_gravity", "0")
        timer.Simple(10, function() RunConsoleCommand("sv_gravity", "600") end)
        ---UNDO---
        RunConsoleCommand("sv_gravity", "600")
        """;

    public AiCodeGeneratorService(
        HttpClient httpClient,
        SettingsService settingsService,
        CommandQueueService commandQueue,
        ILogger<AiCodeGeneratorService> logger)
    {
        _httpClient = httpClient;
        _settingsService = settingsService;
        _commandQueue = commandQueue;
        _logger = logger;
    }

    /// <summary>
    /// Generates Lua code for the given user request.
    /// </summary>
    public async Task<(string ExecutionCode, string UndoCode)> GenerateCodeAsync(
        string userRequest,
        string currentMap = "unknown",
        string? imageContext = null,
        bool includeHistory = true)
    {
        var userContent = new StringBuilder();
        userContent.Append($"Current Map: {currentMap}. Request: {userRequest}");

        if (!string.IsNullOrEmpty(imageContext))
        {
            userContent.Append($"\n[SYSTEM DETECTED IMAGE CONTEXT]: {imageContext}");
        }

        // Include recent command history if enabled
        if (includeHistory && _commandQueue.Preferences.IncludeHistoryInAi)
        {
            var recentCommands = _commandQueue.GetRecentCommands();
            if (recentCommands.Any())
            {
                userContent.Append("\n\n[RECENT COMMAND HISTORY]:\n");
                foreach (var cmd in recentCommands)
                {
                    userContent.Append($"- {cmd.Timestamp:HH:mm:ss}: {cmd.UserPrompt}\n");
                }
            }
        }

        try
        {
            var settings = _settingsService.Settings;
            // Use unfiltered prompt when Private Discord Mode is enabled
            var activePrompt = settings.Safety.PrivateDiscordMode ? PrivateDiscordModePrompt : SystemPrompt;

            var requestBody = new
            {
                model = settings.OpenRouter.Model,
                messages = new[]
                {
                    new { role = "system", content = activePrompt },
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

            var code = jsonDoc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString() ?? "";

            // Clean up markdown if present
            code = code.Replace("```lua", "").Replace("```", "").Trim();

            // Parse execution and undo code
            if (code.Contains("---UNDO---"))
            {
                var parts = code.Split("---UNDO---");
                var executionCode = parts[0].Trim();
                var undoCode = parts.Length > 1 ? parts[1].Trim() : "print(\"No undo code provided\")";
                return (executionCode, undoCode);
            }

            return (code, "print(\"Undo not available for this command\")");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate code");
            return ("print(\"AI Generation Failed\")", "print(\"Undo not available\")");
        }
    }

    /// <summary>
    /// Generates force undo code for a stuck command.
    /// </summary>
    public async Task<string> GenerateForceUndoAsync(CommandEntry command)
    {
        var forceUndoPrompt = $"""
            The following command is still causing problems and needs to be forcefully stopped:

            Original Request: {command.UserPrompt}
            Original Code: {command.ExecutionCode}
            Previous Undo Attempt: {command.UndoCode}

            This is still a problem. Generate comprehensive Lua code to:
            1. Stop ALL timers that might be related
            2. Remove ALL entities that were spawned
            3. Reset ALL player properties to default
            4. Clear ALL screen effects and UI elements
            5. Restore normal game state

            Be aggressive - we need to ensure this effect is completely gone.
            Return ONLY the Lua code to execute, no explanations.
            """;

        try
        {
            var settings = _settingsService.Settings;
            var requestBody = new
            {
                model = settings.OpenRouter.Model,
                messages = new[]
                {
                    new { role = "system", content = "You are a Garry's Mod Lua expert. Generate code to completely stop and reverse problematic effects." },
                    new { role = "user", content = forceUndoPrompt }
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

            var code = jsonDoc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString() ?? "";

            return code.Replace("```lua", "").Replace("```", "").Trim();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate force undo code");
            return "print(\"Force undo generation failed\")";
        }
    }
}