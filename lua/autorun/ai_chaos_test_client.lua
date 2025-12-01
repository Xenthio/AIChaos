-- ai_chaos_test_client.lua
-- Test client companion script for AI Chaos
-- This runs on a separate GMod instance to test commands before sending to the main client

if SERVER then
    -- Create the ConVar first so command-line arguments work
    if not ConVarExists("ai_chaos_test_client") then
        CreateConVar("ai_chaos_test_client", "0", FCVAR_ARCHIVE, "Set to 1 to enable test client mode")
    end
    
    -- Wait a frame for command line arguments to be processed
    timer.Simple(0, function()
        -- Check if this is a test client (launched with +ai_chaos_test_client 1)
        local isTestClient = GetConVar("ai_chaos_test_client")
        if not isTestClient or isTestClient:GetInt() ~= 1 then
            print("[AI Chaos Test] Not a test client (ai_chaos_test_client = " .. (isTestClient and tostring(isTestClient:GetInt()) or "nil") .. "), skipping initialization")
            print("[AI Chaos Test] To enable: Launch GMod with +ai_chaos_test_client 1 or set ai_chaos_test_client 1 in console")
            return
        end

        util.AddNetworkString("AI_TestClient_RunCode")

        -- Try to read URL from data file, fallback to hardcoded URL
        local BASE_URL = "http://localhost:5000"
        local SERVER_URL = "http://localhost:5000/poll/test"
        local POLL_INTERVAL = 1 -- Poll more frequently for test client
        
        -- Attempt to read URL from data file (created by launcher)
        local urlFiles = {"addons/AIChaos/tunnel_url.txt", "addons/AIChaos/ngrok_url.txt"}
        local foundUrl = false
        
        for _, urlFile in ipairs(urlFiles) do
            if file.Exists(urlFile, "GAME") then
                local content = file.Read(urlFile, "GAME")
                if content and content ~= "" then
                    content = string.Trim(content)
                    BASE_URL = content
                    SERVER_URL = content .. "/poll/test"
                    print("[AI Chaos Test] Loaded URL from config: " .. SERVER_URL)
                    foundUrl = true
                    break
                end
            end
        end
        
        if not foundUrl then
            print("[AI Chaos Test] Using default URL: " .. SERVER_URL)
        end

        print("========================================")
        print("[AI Chaos] TEST CLIENT Initialized!")
        print("[AI Chaos] This instance will test commands before the main client")
        print("[AI Chaos] Polling endpoint: " .. SERVER_URL)
        print("========================================")

        -- Helper Function: Report test result back to server
        local function ReportTestResult(commandId, success, errorMsg)
            if commandId == nil or commandId == 0 then return end
            
            local reportUrl = BASE_URL .. "/report/test"
            local body = {
                command_id = commandId,
                success = success,
                error = errorMsg,
                is_test_client = true
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
                        print("[AI Chaos Test] Test result reported for command #" .. tostring(commandId))
                    end
                end,
                failed = function(err)
                    print("[AI Chaos Test] Failed to report test result: " .. tostring(err))
                end
            })
        end

        -- Helper Function: Run the code safely and capture any errors
        -- Uses RunString with handleError=false to properly capture error messages
        local function ExecuteTestCode(code, commandId, cleanupAfterTest)
            print("[AI Chaos Test] Testing generated code...")
            print("[AI Chaos Test] Executing code:\n" .. code)
            
            -- RunString returns error string when handleError is false
            local result = RunString(code, "AI_Chaos_Test_" .. tostring(commandId), false)
            
            -- If result is nil or empty string, execution was successful
            -- If result is a non-empty string, it contains the error message
            -- Note: RunString can return false (boolean) in some error cases
            local success = (result == nil or result == "")
            local errorMsg = nil
            
            if not success then
                if result == false then
                    errorMsg = "Code execution failed (compilation or runtime error)"
                elseif result == true then
                    errorMsg = "Unexpected boolean return from RunString"
                elseif type(result) == "string" then
                    errorMsg = result
                else
                    errorMsg = "Unknown error: " .. tostring(result)
                end
                print("[AI Chaos Test] ✗ Code Error: " .. errorMsg)
            else
                print("[AI Chaos Test] ✓ Code executed successfully!")
            end
            
            ReportTestResult(commandId, success, errorMsg)
            
            -- Cleanup after test if requested
            if cleanupAfterTest then
                timer.Simple(0.5, function()
                    print("[AI Chaos Test] Running cleanup...")
                    RunConsoleCommand("gmod_admin_cleanup")
                end)
            end
        end

        -- Forward declaration
        local PollTestServer 

        -- The Polling Logic
        PollTestServer = function()
            local body = { 
                map = game.GetMap(),
                is_test_client = true
            }

            HTTP({
                method = "POST",
                url = SERVER_URL,
                body = util.TableToJSON(body),
                headers = { 
                    ["Content-Type"] = "application/json",
                    ["ngrok-skip-browser-warning"] = "true"
                },
                
                success = function(code, body, headers)
                    if code == 200 then
                        local data = util.JSONToTable(body)
                        if data and data.has_code then
                            print("[AI Chaos Test] Received code for testing!")
                            ExecuteTestCode(data.code, data.command_id, data.cleanup_after_test)
                        end
                    else
                        -- 204 No Content is expected when there's nothing to test
                        if code ~= 204 then
                            print("[AI Chaos Test] Server Error Code: " .. tostring(code))
                        end
                    end
                    
                    timer.Simple(POLL_INTERVAL, PollTestServer)
                end,

                failed = function(err)
                    print("[AI Chaos Test] Connection Failed: " .. tostring(err))
                    timer.Simple(POLL_INTERVAL, PollTestServer)
                end
            })
        end

        -- Start the polling loop
        print("[AI Chaos Test] Starting Test Polling Loop...")
        PollTestServer()
    end)

else -- CLIENT SIDE CODE
    
    net.Receive("AI_TestClient_RunCode", function()
        local code = net.ReadString()
        local success, err = pcall(function()
            print("[AI Chaos Test] Running test client code:\n" .. code)
            RunString(code)
        end)
        if not success then print("[AI Chaos Test Client Error]", err) end
    end)
end
