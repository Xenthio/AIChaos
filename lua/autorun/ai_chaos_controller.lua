-- ai_chaos_controller.lua

if SERVER then
    util.AddNetworkString("AI_RunClientCode")

    -- Try to read URL from data file, fallback to hardcoded URL
                local SERVER_URL = "https://aichaos-apigfg00.loca.lt/poll" -- Auto-configured by launcher
    local POLL_INTERVAL = 2 -- Seconds to wait between requests
    
    -- Attempt to read URL from data file (created by launcher)
    -- Supports both ngrok_url.txt and tunnel_url.txt
    local urlFiles = {"addons/AIChaos/tunnel_url.txt", "addons/AIChaos/ngrok_url.txt"}
    local foundUrl = false
    
    for _, urlFile in ipairs(urlFiles) do
        if file.Exists(urlFile, "GAME") then
            local content = file.Read(urlFile, "GAME")
            if content and content ~= "" then
                -- Trim whitespace and add /poll endpoint
                content = string.Trim(content)
                SERVER_URL = content .. "/poll"
                print("[AI Chaos] Loaded URL from config: " .. SERVER_URL)
                foundUrl = true
                break
            end
        end
    end
    
    if not foundUrl then
        print("[AI Chaos] Using hardcoded URL: " .. SERVER_URL)
        print("[AI Chaos] Run a launcher script to auto-configure!")
        print("[AI Chaos] Available launchers:")
        print("[AI Chaos]   - start_with_localtunnel.py (No account needed!)")
        print("[AI Chaos]   - start_with_ngrok.py")
    end

    print("[AI Chaos] Server Initialized!")
    print("[AI Chaos] Polling endpoint: " .. SERVER_URL)

    -- 1. Helper Function: Send code to client
    function RunOnClient(codeString)
        net.Start("AI_RunClientCode")
        net.WriteString(codeString)
        net.Broadcast()
    end

    -- 2. Helper Function: Run the code safely
    local function ExecuteAICode(code)
        print("[AI Chaos] Running generated code...")
        local success, err = pcall(function()
            -- Print whole code for debugging
            print("[AI Chaos] Executing code:\n" .. code)
            RunString(code)
        end)

        if success then
            --PrintMessage(HUD_PRINTTALK, "[AI] Event triggered!")
        else
            PrintMessage(HUD_PRINTTALK, "[AI] Code Error: " .. tostring(err))
            print("[AI Error]", err)
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
            headers = { ["Content-Type"] = "application/json" },
            
            -- ON SUCCESS
            success = function(code, body, headers)
                if code == 200 then
                    local data = util.JSONToTable(body)
                    if data and data.has_code then
                        print("[AI Chaos] Received code!")
                        ExecuteAICode(data.code)
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