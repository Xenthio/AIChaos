-- ai_chaos_controller.lua

if SERVER then
    -- Create the test client ConVar first (shared with ai_chaos_test_client.lua)
    if not ConVarExists("ai_chaos_test_client") then
        CreateConVar("ai_chaos_test_client", "0", FCVAR_ARCHIVE, "Set to 1 to enable test client mode")
    end
    
    -- Wait a frame for command line arguments to be processed
    timer.Simple(0, function()
        -- Check if this is a test client - if so, skip main controller initialization
        local isTestClient = GetConVar("ai_chaos_test_client")
        if isTestClient and isTestClient:GetInt() == 1 then
            print("[AI Chaos] Test client detected - main controller disabled (use ai_chaos_test_client.lua instead)")
            return
        end
        
        util.AddNetworkString("AI_RunClientCode")

        -- Try to read URL from data file, fallback to hardcoded URL
        local BASE_URL = "https://voluntarily-paterfamiliar-jeanie.ngrok-free.dev" -- Auto-configured by launcher
        local SERVER_URL = "https://voluntarily-paterfamiliar-jeanie.ngrok-free.dev/poll" -- Auto-configured by launcher
        local POLL_INTERVAL = 2 -- Seconds to wait between requests
        
        -- Attempt to read URL from data file (created by launcher)
        -- Supports both ngrok_url.txt and tunnel_url.txt
        local urlFiles = {"addons/AIChaos/tunnel_url.txt", "addons/AIChaos/ngrok_url.txt"}
        local foundUrl = false
        
        for _, urlFile in ipairs(urlFiles) do
            if file.Exists(urlFile, "GAME") then
                local content = file.Read(urlFile, "GAME")
                if content and content ~= "" then
                    -- Trim whitespace - content should be the base URL (without /poll)
                    content = string.Trim(content)
                    BASE_URL = content
                    SERVER_URL = content .. "/poll"
                    print("[AI Chaos] Loaded URL from config: " .. SERVER_URL)
                    foundUrl = true
                    break
                end
            end
        end
        
        if not foundUrl then
            print("[AI Chaos] Using default URL: " .. SERVER_URL)
            print("[AI Chaos] Run a launcher or start a tunnel from the Setup page to connect!")
        end

        print("[AI Chaos] Server Initialized!")
        print("[AI Chaos] Polling endpoint: " .. SERVER_URL)

    -- 1. Helper Function: Send code to client
    function RunOnClient(codeString)
        net.Start("AI_RunClientCode")
        net.WriteString(codeString)
        net.Broadcast()
    end
    
    -- 2. Workshop Helper Functions
    -- These functions allow downloading and spawning models from Steam Workshop
    -- Note: These require the AllowWorkshopDownload setting to be enabled
    
    -- Download and mount a workshop addon, then spawn the first valid model found
    -- workshopId: The Steam Workshop ID (e.g., "104691717")
    -- callback: Optional callback function that receives the spawned entity (or nil on failure)
    function DownloadAndSpawnWorkshopModel(workshopId, callback)
        if not workshopId then
            print("[AI Chaos] Workshop: No workshop ID provided")
            if callback then callback(nil) end
            return
        end
        
        -- Check if workshop downloads are allowed
        local checkUrl = BASE_URL .. "/api/settings/workshop-allowed"
        HTTP({
            method = "GET",
            url = checkUrl,
            headers = { 
                ["ngrok-skip-browser-warning"] = "true"
            },
            success = function(code, body, headers)
                if code == 200 then
                    local data = util.JSONToTable(body)
                    if not (data and data.allowed == true) then
                        print("[AI Chaos] Workshop: Downloads are disabled in settings")
                        if callback then callback(nil) end
                        return
                    end
                    
                    -- Permission granted, proceed with download
                    print("[AI Chaos] Workshop: Attempting to download workshop item " .. workshopId)
                    
                    steamworks.FileInfo(workshopId, function(result)
                        if not result then
                            print("[AI Chaos] Workshop: Failed to get info for " .. workshopId)
                            if callback then callback(nil) end
                            return
                        end
                        
                        print("[AI Chaos] Workshop: Found addon: " .. (result.title or "Unknown"))
                        
                        -- Download the addon
                        steamworks.Download(workshopId, true, function(path)
                            if not path then
                                print("[AI Chaos] Workshop: Download failed for " .. workshopId)
                                if callback then callback(nil) end
                                return
                            end
                            
                            print("[AI Chaos] Workshop: Downloaded to " .. path)
                            
                            -- Mount the addon
                            if steamworks.ShouldMountAddon(workshopId) then
                                steamworks.SetShouldMountAddon(workshopId, true)
                                print("[AI Chaos] Workshop: Addon mounted")
                                
                                -- Find and spawn the first valid model
                                local ent = FindAndSpawnFirstWorkshopModel(workshopId)
                                if callback then callback(ent) end
                            else
                                print("[AI Chaos] Workshop: Failed to mount addon")
                                if callback then callback(nil) end
                            end
                        end)
                    end)
                end
            end,
            failed = function(err)
                print("[AI Chaos] Workshop: Failed to check permission: " .. tostring(err))
                if callback then callback(nil) end
            end
        })
    end
    
    -- Find the first valid model in a workshop addon and spawn it in front of the player
    -- Excludes gesture models and other non-visible models
    function FindAndSpawnFirstWorkshopModel(workshopId)
        local player = Entity(1)
        if not IsValid(player) then
            print("[AI Chaos] Workshop: No valid player found")
            return nil
        end
        
        -- Get all files from the workshop addon
        local files, folders = file.Find("models/*.mdl", "workshop")
        
        -- Filter out gesture and other non-visible models
        local validModel = nil
        for _, modelPath in ipairs(files or {}) do
            local lowerPath = string.lower(modelPath)
            -- Skip gesture models, reference models, and other typically invisible models
            if not (string.find(lowerPath, "gesture") or 
                    string.find(lowerPath, "reference") or
                    string.find(lowerPath, "ref.mdl") or
                    string.find(lowerPath, "phys.mdl")) then
                validModel = "models/" .. modelPath
                break
            end
        end
        
        if not validModel then
            print("[AI Chaos] Workshop: No valid models found in workshop addon")
            return nil
        end
        
        print("[AI Chaos] Workshop: Spawning model: " .. validModel)
        
        -- Spawn the model in front of the player
        local pos = player:GetPos() + player:GetForward() * 100 + Vector(0, 0, 50)
        local ang = player:GetAngles()
        ang.p = 0 -- Keep it upright
        
        local ent = ents.Create("prop_physics")
        ent:SetModel(validModel)
        ent:SetPos(pos)
        ent:SetAngles(ang)
        ent:Spawn()
        ent:Activate()
        
        print("[AI Chaos] Workshop: Model spawned successfully")
        return ent
    end
    
    -- Browse and list all models from workshop addons (for AI preparation phase)
    -- Returns a table of model paths from all mounted workshop addons
    function BrowseWorkshopModels()
        local models = {}
        local files, folders = file.Find("models/*.mdl", "workshop")
        
        for _, modelPath in ipairs(files or {}) do
            local fullPath = "models/" .. modelPath
            local lowerPath = string.lower(fullPath)
            
            -- Filter out non-visible models
            if not (string.find(lowerPath, "gesture") or 
                    string.find(lowerPath, "reference") or
                    string.find(lowerPath, "ref.mdl") or
                    string.find(lowerPath, "phys.mdl")) then
                table.insert(models, fullPath)
            end
        end
        
        return models
    end
    
    -- Helper Function: Report execution result back to server (with optional captured data)
    -- Note: commandId can be negative for interactive sessions, nil/0 is not valid
    local function ReportResult(commandId, success, errorMsg, resultData)
        if commandId == nil or commandId == 0 then return end
        
        local reportUrl = BASE_URL .. "/report"
        local body = {
            command_id = commandId,
            success = success,
            error = errorMsg,
            result_data = resultData
        }
        
        HTTP({
            method = "POST",
            url = reportUrl,
            body = util.TableToJSON(body),
            headers = { 
                ["Content-Type"] = "application/json",
                ["ngrok-skip-browser-warning"] = "true"
            },
            success = function(code, body, headers)
                if code == 200 then
                    print("[AI Chaos] Result reported for command #" .. tostring(commandId))
                end
            end,
            failed = function(err)
                print("[AI Chaos] Failed to report result: " .. tostring(err))
            end
        })
    end

    -- 2. Helper Function: Run the code safely using CompileString + pcall for proper error messages
    -- This approach captures both syntax errors (from CompileString) and runtime errors (from pcall)
    local function ExecuteAICode(code, commandId)
        print("[AI Chaos] Running generated code...")
        
        -- Clear any previous captured data
        _AI_CAPTURED_DATA = nil
        
        -- Print whole code for debugging
        print("[AI Chaos] Executing code:\n" .. code)
        
        local chunkName = "AI_Chaos_" .. tostring(commandId or 0)
        
        -- Step 1: Try to compile the code first (catches syntax errors)
        -- CompileString returns error string if compilation fails, or a function if successful
        local compiled = CompileString(code, chunkName, false)
        
        local success = false
        local errorMsg = nil
        
        if type(compiled) == "string" then
            -- Compilation failed - compiled contains the error message
            errorMsg = compiled
        elseif type(compiled) == "function" then
            -- Compilation succeeded - now execute with pcall to catch runtime errors
            local ok, runtimeErr = pcall(compiled)
            if ok then
                success = true
            else
                -- Runtime error - runtimeErr contains the actual error message
                errorMsg = tostring(runtimeErr)
            end
        else
            -- Unexpected return type from CompileString
            errorMsg = "Unexpected CompileString return type: " .. type(compiled)
        end
        
        -- Get captured data if any (used by interactive mode)
        local capturedData = _AI_CAPTURED_DATA
        _AI_CAPTURED_DATA = nil
        
        if success then
            --PrintMessage(HUD_PRINTTALK, "[AI] Event triggered!")
            ReportResult(commandId, true, nil, capturedData)
        else
            PrintMessage(HUD_PRINTTALK, "[AI] Code Error: " .. errorMsg)
            print("[AI Error]", errorMsg)
            ReportResult(commandId, false, errorMsg, capturedData)
        end
    end

    -- Forward declaration
    local PollServer 

    -- 3. The Polling Logic
    PollServer = function()
        -- print("[AI Chaos] Polling...") -- Uncomment to see spam in console
        
        local body = { map = game.GetMap() }

        HTTP({
            method = "POST",
            url = SERVER_URL,
            body = util.TableToJSON(body),
            headers = { 
                ["Content-Type"] = "application/json",
                ["ngrok-skip-browser-warning"] = "true"
            },
            
            -- ON SUCCESS
            success = function(code, body, headers)
                if code == 200 then
                    local data = util.JSONToTable(body)
                    if data and data.has_code then
                        print("[AI Chaos] Received code!")
                        ExecuteAICode(data.code, data.command_id)
                    end
                else
                    print("[AI Chaos] Server Error Code: " .. tostring(code))
                end
                
                -- Schedule the NEXT poll only after this one finishes
                timer.Simple(POLL_INTERVAL, PollServer)
            end,

            -- ON FAILURE (Important: If Python is closed, this runs)
            failed = function(err)
                print("[AI Chaos] Connection Failed: " .. tostring(err))
                -- Schedule the NEXT poll even if this one failed
                timer.Simple(POLL_INTERVAL, PollServer)
            end
        })
    end

    -- Start the loop
    print("[AI Chaos] Starting Polling Loop...")
    PollServer()
    
    end) -- End of timer.Simple callback

else -- CLIENT SIDE CODE
    
    net.Receive("AI_RunClientCode", function()
        local code = net.ReadString()
        local success, err = pcall(function()
            -- print whole code for debugging
            print("[AI Chaos] Running client code:\n" .. code)
            RunString(code)
        end)
        if not success then print("[AI Client Error]", err) end
    end)
end