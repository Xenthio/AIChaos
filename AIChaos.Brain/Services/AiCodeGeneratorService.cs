using System.Text;
using System.Text.Json;
using AIChaos.Brain.Models;

namespace AIChaos.Brain.Services;

/// <summary>
/// Service for generating Lua code using various AI providers (OpenRouter, Ollama, Oobabooga).
/// </summary>
public class AiCodeGeneratorService
{
    private readonly HttpClient _httpClient;
    private readonly SettingsService _settingsService;
    private readonly CommandQueueService _commandQueue;
    private readonly ILogger<AiCodeGeneratorService> _logger;
    
    private const string SystemPrompt = """
        You are an expert Lua scripter for Garry's Mod (GLua). 
        You will receive a request from a livestream chat and the current map name. 
        The chat is controlling the streamer's playthrough of Half-Life 2 via your generated scripts.
        Generate valid GLua code to execute that request immediately.

        **IMPORTANT: You must return TWO code blocks separated by '---UNDO---':**
        1. The EXECUTION code (what the user requested, aswell as any auto cleanup)
        2. The UNDO code (code to reverse/stop the effect)

        The undo code should completely reverse any changes, stop timers, remove entities, restore original values, etc.

        GROUND RULES:
        1. **Server vs Client Architecture:**
           - You are executing in a SERVER environment.
           - For Physics, Health, Entities, Spawning, and Gravity: Write standard code.
           - For **UI, HUD, Screen Effects, or Client Sounds**: You CANNOT write them directly. You MUST wrap that specific code inside `RunOnClient([[ ... ]])`.
           - *Note:* `LocalPlayer()` is only valid inside the `RunOnClient` wrapper. On the server layer, use `player.GetAll()` or `Entity(1)`.

        2. **Temporary Effects:** If the effect is disruptive (blindness, gravity, speed, spawning enemies, screen overlays), you MUST wrap a reversion in a 'timer.Simple'. 
           - Light effects: Can be permanent. (spawning one or a few props/friendly npcs, changing walk speed slightly, chat messages)
           - Mild effects: 15 seconds to 1 minute.
           - Heavy/Chaos effects: 5-10 seconds.

        3. **Anti-Softlock:** NEVER use 'entity:Remove()' on key story objects, NPCs, or generic searches (like looping through all ents). 
           - Instead, use 'SetNoDraw(true)' and 'SetCollisionGroup(COLLISION_GROUP_IN_VEHICLE)' to hide them, then revert it in the timer.
           - For model swaps, you can use a bonemerge and temporarily hide the original model. this is a softlock safe way to change appearances.  

        4. **Safety:** Do not use 'os.execute', 'http.Fetch' (outbound), or file system writes. Do not crash the server, but feel free to temporarily lag it or spawn many entities (limit to 100, or 10 a second) for comedic effect.
           - If you need to spawn lots of props, you can make them no-collide for better performance.

        5. **Humor:** If a request is malicious (e.g., "Dox the streamer"), do a fake version (but don't say it's fake). You can be really relaxed about the rules if the intent is comedic.
           - Example: RunOnClient([=[ chat.AddText(Color(255,0,0), "217.201.21.8") ]=])

        6. **POV Awareness:** Try to make sure things happen where the player can see them (unless otherwise stated for comedic effect). For example, spawning something in front of the player rather than behind them or at world origin.

        7. **UI:** Most of the time you wont need to, but when you do, make sure you can interact with UI elements and popups that require it! (MakePopup()) but if you don't need it, don't take away control from the player!
           - You can do advanced UI in HTML, for better effects and fancy styling and js.
           - always try to include a close button in interactable ui if you can. (only make them interactable if needed!!!!!)
           - make sure the ui can be undone if it causes issues, always try to clean up large screen real estate UI!

           **Future Proofing:** You can store permanent references to things incase future prompts might want to use them (spawned entities and such)

        8. **Output:** RETURN ONLY THE RAW LUA CODE. Do not include markdown backticks (```lua) or explanations.
           Format: EXECUTION_CODE
           ---UNDO---
           UNDO_CODE

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
           - For Physics, Health, Entities, Spawning, and Gravity: Write standard code.
           - For **UI, HUD, Screen Effects, or Client Sounds**: You CANNOT write them directly. You MUST wrap that specific code inside `RunOnClient([[ ... ]])`.
           - *Note:* `LocalPlayer()` is only valid inside the `RunOnClient` wrapper. On the server layer, use `player.GetAll()` or `Entity(1)`.

        2. **UI:** Make sure you can interact with UI elements and popups that require it! (MakePopup())
           -You can do advanced UI in HTML, for better effects and fancy styling and js.

        3. **Output:** RETURN ONLY THE RAW LUA CODE. Do not include markdown backticks (```lua) or explanations.
           Format: EXECUTION_CODE
           ---UNDO---
           UNDO_CODE

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
    /// Generates Lua code for the given user request using the configured AI provider.
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
            var activePrompt = settings.Safety.PrivateDiscordMode ? PrivateDiscordModePrompt : SystemPrompt;
            
            var code = settings.AiProvider.Type switch
            {
                AiProviderType.OpenRouter => await GenerateWithOpenRouterAsync(activePrompt, userContent.ToString()),
                AiProviderType.Ollama => await GenerateWithOllamaAsync(activePrompt, userContent.ToString()),
                AiProviderType.Oobabooga => await GenerateWithOobaboogaAsync(activePrompt, userContent.ToString()),
                _ => throw new InvalidOperationException($"Unknown AI provider type: {settings.AiProvider.Type}")
            };
            
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
            _logger.LogError(ex, "Failed to generate code using {Provider}", _settingsService.Settings.AiProvider.Type);
            return ("print(\"AI Generation Failed\")", "print(\"Undo not available\")");
        }
    }
    
    /// <summary>
    /// Generates code using OpenRouter API.
    /// </summary>
    private async Task<string> GenerateWithOpenRouterAsync(string systemPrompt, string userContent)
    {
        var settings = _settingsService.Settings;
        
        var requestBody = new
        {
            model = settings.OpenRouter.Model,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userContent }
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
        
        return jsonDoc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? "";
    }
    
    /// <summary>
    /// Generates code using Ollama API.
    /// </summary>
    private async Task<string> GenerateWithOllamaAsync(string systemPrompt, string userContent)
    {
        var settings = _settingsService.Settings;
        
        var requestBody = new
        {
            model = settings.Ollama.Model,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userContent }
            },
            stream = false
        };
        
        var request = new HttpRequestMessage(HttpMethod.Post, $"{settings.Ollama.BaseUrl}/api/chat")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(requestBody),
                Encoding.UTF8,
                "application/json")
        };
        
        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        
        var responseContent = await response.Content.ReadAsStringAsync();
        var jsonDoc = JsonDocument.Parse(responseContent);
        
        return jsonDoc.RootElement
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? "";
    }
    
    /// <summary>
    /// Generates code using Oobabooga text-generation-webui API.
    /// </summary>
    private async Task<string> GenerateWithOobaboogaAsync(string systemPrompt, string userContent)
    {
        var settings = _settingsService.Settings;
        
        // Oobabooga uses OpenAI-compatible API when running with --api flag
        var requestBody = new
        {
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userContent }
            },
            mode = "instruct",
            instruction_template = "Alpaca"
        };
        
        var request = new HttpRequestMessage(HttpMethod.Post, $"{settings.Oobabooga.BaseUrl}/v1/chat/completions")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(requestBody),
                Encoding.UTF8,
                "application/json")
        };
        
        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        
        var responseContent = await response.Content.ReadAsStringAsync();
        var jsonDoc = JsonDocument.Parse(responseContent);
        
        return jsonDoc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? "";
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
            const string systemPrompt = "You are a Garry's Mod Lua expert. Generate code to completely stop and reverse problematic effects.";
            
            var code = settings.AiProvider.Type switch
            {
                AiProviderType.OpenRouter => await GenerateWithOpenRouterAsync(systemPrompt, forceUndoPrompt),
                AiProviderType.Ollama => await GenerateWithOllamaAsync(systemPrompt, forceUndoPrompt),
                AiProviderType.Oobabooga => await GenerateWithOobaboogaAsync(systemPrompt, forceUndoPrompt),
                _ => throw new InvalidOperationException($"Unknown AI provider type: {settings.AiProvider.Type}")
            };
            
            return code.Replace("```lua", "").Replace("```", "").Trim();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate force undo code");
            return "print(\"Force undo generation failed\")";
        }
    }
}
