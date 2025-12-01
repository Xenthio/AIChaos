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

    -- 2. Helper Function: Run the code safely using RunString with handleError=false
    local function ExecuteAICode(code, commandId)
        print("[AI Chaos] Running generated code...")
        
        -- Clear any previous captured data
        _AI_CAPTURED_DATA = nil
        
        -- Print whole code for debugging
        print("[AI Chaos] Executing code:\n" .. code)
        
        -- RunString returns error string when handleError is false
        local result = RunString(code, "AI_Chaos_" .. tostring(commandId or 0), false)
        
        -- Get captured data if any (used by interactive mode)
        local capturedData = _AI_CAPTURED_DATA
        _AI_CAPTURED_DATA = nil
        
        -- If result is nil or empty string, execution was successful
        -- If result is a non-empty string, it contains the error message
        -- Note: RunString can return false (boolean) in some error cases
        local success = (result == nil or result == "")
        
        if success then
            --PrintMessage(HUD_PRINTTALK, "[AI] Event triggered!")
            ReportResult(commandId, true, nil, capturedData)
        else
            local errorMsg
            if result == false then
                errorMsg = "Code execution failed (compilation or runtime error)"
            elseif result == true then
                errorMsg = "Unexpected boolean return from RunString"
            elseif type(result) == "string" then
                errorMsg = result
            else
                errorMsg = "Unknown error: " .. tostring(result)
            end
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