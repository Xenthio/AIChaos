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
    
    -- Workshop Helper Functions
    -- Table to track downloaded workshop addons
    _G._AI_WORKSHOP_ADDONS = _G._AI_WORKSHOP_ADDONS or {}
    
    -- Helper: Check if a model is a gesture or invisible model
    local function IsValidSpawnModel(modelPath)
        if not modelPath then return false end
        local lower = string.lower(modelPath)
        -- Exclude gesture models and other non-visual models
        if string.find(lower, "gesture") then return false end
        if string.find(lower, "gib") then return false end
        if string.find(lower, "bone") then return false end
        return true
    end
    
    -- Helper: Recursively find all .mdl files in a directory and its subdirectories
    local function FindModelsRecursive(basePath, modelsTable)
        local files, dirs = file.Find(basePath .. "*", "GAME")
        if files then
            for _, fileName in ipairs(files) do
                if string.EndsWith(fileName, ".mdl") then
                    local modelPath = basePath .. fileName
                    table.insert(modelsTable, modelPath)
                end
            end
        end
        if dirs then
            for _, dirName in ipairs(dirs) do
                -- Skip "." and ".."
                if dirName ~= "." and dirName ~= ".." then
                    FindModelsRecursive(basePath .. dirName .. "/", modelsTable)
                end
            end
        end
    end
    
    -- Helper: Get all models from a Workshop addon
    -- Note: Workshop content is accessed via "GAME" path after mounting
    function GetWorkshopModels(workshopId)
        local models = {}
        local searchPaths = {"models/", "models/props/", "models/player/", "models/weapons/"}
        
        -- First ensure the addon is mounted
        if not _G._AI_WORKSHOP_ADDONS[workshopId] then
            print("[AI Chaos] GetWorkshopModels: Workshop addon " .. workshopId .. " not yet mounted")
            return models
        end
        
        for _, basePath in ipairs(searchPaths) do
            FindModelsRecursive(basePath, models)
        end
        
        return models
    end
    
    -- Helper: Get all models from all mounted addons
    function GetAllMountedModels()
        local models = {}
        local searchPaths = {"models/", "models/props/", "models/player/", "models/weapons/"}
        
        for _, basePath in ipairs(searchPaths) do
            FindModelsRecursive(basePath, models)
        end
        
        return models
    end
    
    -- Mount a Workshop addon at runtime
    function MountWorkshopAddon(workshopId)
        if not workshopId or workshopId == "" then 
            print("[AI Chaos] MountWorkshopAddon: No workshop ID provided")
            return false 
        end
        
        -- Validate it's a numeric string
        if not string.match(workshopId, "^%d+$") then
            print("[AI Chaos] MountWorkshopAddon: Invalid workshop ID format (must be numeric): " .. tostring(workshopId))
            return false
        end
        
        -- Check if already mounted
        if _G._AI_WORKSHOP_ADDONS[workshopId] then
            print("[AI Chaos] Workshop addon " .. workshopId .. " already mounted")
            return true
        end
        
        -- Use steamworks.Download to download and mount the addon
        print("[AI Chaos] Downloading and mounting workshop addon: " .. workshopId)
        steamworks.Download(workshopId, true, function(path)
            if path then
                print("[AI Chaos] Workshop addon " .. workshopId .. " downloaded to: " .. path)
                _G._AI_WORKSHOP_ADDONS[workshopId] = path
                game.MountGMA(path)
            else
                print("[AI Chaos] Failed to download workshop addon: " .. workshopId)
            end
        end)
        
        return true
    end
    
    -- Download and spawn the first valid model from a Workshop addon
    function DownloadAndSpawnWorkshopModel(workshopId, spawnPos)
        if not workshopId or workshopId == "" then 
            print("[AI Chaos] DownloadAndSpawnWorkshopModel: No workshop ID provided")
            return nil 
        end
        
        -- Validate it's a numeric string
        if not string.match(workshopId, "^%d+$") then
            print("[AI Chaos] DownloadAndSpawnWorkshopModel: Invalid workshop ID format (must be numeric): " .. tostring(workshopId))
            return nil
        end
        
        -- Default spawn position with Entity(1) validation
        if not spawnPos then
            local player = Entity(1)
            if not IsValid(player) then
                spawnPos = Vector(0, 0, 100)
            else
                spawnPos = player:GetPos() + player:GetForward() * 100 + Vector(0, 0, 50)
            end
        end
        
        print("[AI Chaos] Attempting to spawn model from workshop addon: " .. workshopId)
        
        -- Download and mount the addon first
        steamworks.Download(workshopId, true, function(path)
            if not path then
                print("[AI Chaos] Failed to download workshop addon: " .. workshopId)
                return
            end
            
            print("[AI Chaos] Workshop addon downloaded: " .. path)
            _G._AI_WORKSHOP_ADDONS[workshopId] = path
            game.MountGMA(path)
            
            -- Use a retry mechanism to check if mounting succeeded
            local function TrySpawnModel(attempts)
                attempts = attempts or 0
                if attempts > 5 then
                    print("[AI Chaos] Failed to mount/spawn after multiple attempts")
                    return
                end
                
                local models = GetWorkshopModels(workshopId)
                
                if #models == 0 then
                    print("[AI Chaos] No models found yet, retrying... (attempt " .. attempts .. ")")
                    timer.Simple(0.5, function() TrySpawnModel(attempts + 1) end)
                    return
                end
                
                -- Find first valid spawn model
                local selectedModel = nil
                for _, modelPath in ipairs(models) do
                    if IsValidSpawnModel(modelPath) then
                        selectedModel = modelPath
                        break
                    end
                end
                
                if not selectedModel then
                    print("[AI Chaos] No valid spawn models found in workshop addon (all models were gestures/gibs/bones)")
                    return
                end
                
                print("[AI Chaos] Spawning model: " .. selectedModel)
                local ent = ents.Create("prop_physics")
                if IsValid(ent) then
                    local success, err = pcall(function()
                        ent:SetModel(selectedModel)
                    end)
                    if not success then
                        print("[AI Chaos] Failed to set model: " .. tostring(err))
                        ent:Remove()
                        return
                    end
                    ent:SetPos(spawnPos)
                    ent:Spawn()
                    print("[AI Chaos] Successfully spawned workshop model!")
                    
                    -- Store reference for potential future use
                    _G._AI_LAST_WORKSHOP_SPAWN = ent
                end
            end
            
            -- Start attempting to spawn
            TrySpawnModel(0)
        end)
        
        -- Return a reference to track spawning (async operation)
        return true
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