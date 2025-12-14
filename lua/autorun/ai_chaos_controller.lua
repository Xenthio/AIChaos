-- ai_chaos_controller.lua
-- Workshop helpers are now in ai_chaos_workshop_helpers.lua

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
        local BASE_URL = "http://localhost:5000"
        local SERVER_URL = "http://localhost:5000/poll"
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

    -- Track current map for level change detection
    local currentMap = game.GetMap()
    local isLevelChanging = false
    
    -- Forward declaration for ExecuteAICode (used by CheckPendingReruns)
    local ExecuteAICode

    -- 1. Helper Function: Send code to client
    function RunOnClient(codeString)
        net.Start("AI_RunClientCode")
        net.WriteString(codeString)
        net.Broadcast()
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
    
    -- Helper Function: Write shutdown timestamp to file
    -- This is called during ShutDown - file writes are synchronous so they complete
    local function WriteShutdownTimestamp()
        local timestamp = os.time()
        file.Write("ai_chaos_shutdown.txt", tostring(timestamp))
        print("[AI Chaos] Shutdown timestamp written: " .. tostring(timestamp))
    end
    
    -- Helper Function: Read and clear shutdown timestamp file
    -- Returns the timestamp if found, nil otherwise
    local function ReadAndClearShutdownTimestamp()
        if file.Exists("ai_chaos_shutdown.txt", "DATA") then
            local content = file.Read("ai_chaos_shutdown.txt", "DATA")
            file.Delete("ai_chaos_shutdown.txt")
            if content and content ~= "" then
                local timestamp = tonumber(content)
                if timestamp then
                    print("[AI Chaos] Found shutdown timestamp: " .. tostring(timestamp))
                    return timestamp
                end
            end
        end
        return nil
    end
    
    -- Helper Function: Check for and execute pending re-runs after level load
    local function CheckPendingReruns()
        -- Check if there was a shutdown before this load
        local shutdownTimestamp = ReadAndClearShutdownTimestamp()
        
        local rerunsUrl = BASE_URL .. "/pending-reruns"
        local body = {}
        
        if shutdownTimestamp then
            body.shutdown_timestamp = shutdownTimestamp
            print("[AI Chaos] Sending shutdown timestamp to server: " .. tostring(shutdownTimestamp))
        end
        
        HTTP({
            method = "POST",
            url = rerunsUrl,
            body = util.TableToJSON(body),
            headers = { 
                ["Content-Type"] = "application/json",
                ["ngrok-skip-browser-warning"] = "true"
            },
            success = function(code, responseBody, headers)
                if code == 200 then
                    local data = util.JSONToTable(responseBody)
                    -- Note: ASP.NET Core uses camelCase by default (pendingReruns, not PendingReruns)
                    local reruns = data and (data.pendingReruns or data.PendingReruns)
                    if reruns and #reruns > 0 then
                        print("[AI Chaos] " .. #reruns .. " command(s) to re-run after level load")
                        for _, rerun in ipairs(reruns) do
                            -- Schedule the re-run with the specified delay (camelCase: delaySeconds, commandId, code)
                            local delay = rerun.delaySeconds or rerun.DelaySeconds or 5
                            local cmdId = rerun.commandId or rerun.CommandId
                            local cmdCode = rerun.code or rerun.Code
                            timer.Simple(delay, function()
                                print("[AI Chaos] Re-running command #" .. tostring(cmdId) .. " after level load")
                                ExecuteAICode(cmdCode, cmdId)
                            end)
                        end
                    else
                        print("[AI Chaos] No pending re-runs")
                    end
                end
            end,
            failed = function(err)
                print("[AI Chaos] Failed to check pending reruns: " .. tostring(err))
            end
        })
    end

    -- 2. Helper Function: Run the code safely using CompileString + pcall for proper error messages
    -- This approach captures both syntax errors (from CompileString) and runtime errors (from pcall)
    ExecuteAICode = function(code, commandId)
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
    
    -- Hook for detecting level/map changes (fires when server is shutting down)
    -- Just write timestamp to file - file writes are synchronous so they complete before exit
    hook.Add("ShutDown", "AI_Chaos_LevelChange", function()
        print("[AI Chaos] ShutDown hook fired - writing timestamp to file")
        isLevelChanging = true
        WriteShutdownTimestamp()
    end)
    
    -- Check for pending re-runs when script loads (fires after map load/save load)
    -- Use a 3-second delay to ensure the server has had time to process any previous commands
    --timer.Simple(3, function()
        print("[AI Chaos] Checking for pending re-runs after load...")
        CheckPendingReruns()
    --end)
    
    -- Track map for detecting changes in poll loop (backup detection)
    local lastKnownMap = game.GetMap()

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