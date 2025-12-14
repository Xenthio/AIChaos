-- ai_chaos_workshop_helpers.lua
-- Workshop & Asset Helper Functions for AI Chaos
-- These functions allow AI to download and use workshop content at runtime
-- SHARED CODE - runs on both CLIENT and SERVER

-- ============================================================================
-- WORKSHOP & ASSET HELPER FUNCTIONS
-- ============================================================================

-- Setup network strings
if SERVER then
    util.AddNetworkString("AI_RequestWorkshopDownload")
    util.AddNetworkString("AI_WorkshopDownloadComplete")
    util.AddNetworkString("AI_RunAutorunOnClient")
    util.AddNetworkString("AI_ClientDetectedNPCs")
end

-- Table to track pending workshop downloads
_AI_PENDING_DOWNLOADS = _AI_PENDING_DOWNLOADS or {}

-- Baseline state - captures what's registered when GMod starts
-- Persists across map changes but resets on GMod restart
if not _AI_BASELINE_STATE then
    _AI_BASELINE_STATE = {
        npcs = table.Copy(list.Get("NPC")),
        weapons = table.Copy(list.Get("Weapon")),
        entities = table.Copy(list.Get("SpawnableEntities"))
    }
    print("[AI Workshop] Captured baseline state: " .. table.Count(_AI_BASELINE_STATE.npcs) .. " NPCs, " .. table.Count(_AI_BASELINE_STATE.weapons) .. " weapons, " .. table.Count(_AI_BASELINE_STATE.entities) .. " entities")
end

-- Helper: Check if a model path looks valid (not a gesture/invisible model)
local function IsValidSpawnableModel(modelPath)
    if not modelPath or modelPath == "" then return false end
    modelPath = string.lower(modelPath)
    
    -- Skip gesture/animation models (use path separators and specific patterns)
    if string.find(modelPath, "/gestures/") then return false end
    if string.find(modelPath, "/animations/") then return false end
    if string.find(modelPath, "/poses/") then return false end
    if string.find(modelPath, "_gesture") then return false end
    if string.find(modelPath, "_anim%.mdl") then return false end -- e.g. walk_anim.mdl
    
    -- Skip invisible/error models
    if string.find(modelPath, "invisible") then return false end
    if string.find(modelPath, "error%.mdl") then return false end
    if string.find(modelPath, "null%.mdl") then return false end
    
    -- Prefer props, ragdolls, and NPCs
    if string.find(modelPath, "props") then return true end
    if string.find(modelPath, "ragdoll") then return true end
    if string.find(modelPath, "npc") then return true end
    if string.find(modelPath, "player") then return true end
    if string.find(modelPath, "vehicle") then return true end
    if string.find(modelPath, "weapons") then return true end
    
    -- Accept any .mdl file as fallback
    return string.EndsWith(modelPath, ".mdl")
end

-- Download and mount a workshop addon at runtime
-- callback(success, mountedPath) - called when download completes
-- NOTE: This handles CLIENT/SERVER automatically via networking
function DownloadAndMountWorkshopAddon(workshopId, callback)
    workshopId = tostring(workshopId)
    print("[AI Workshop] Downloading addon: " .. workshopId)
    
    if CLIENT then
        -- CLIENT: Actually download the workshop content
        if not steamworks then
            print("[AI Workshop] steamworks library not available!")
            if callback then callback(false, nil) end
            return
        end
        
        steamworks.DownloadUGC(workshopId, function(path, file)
            if not path then
                print("[AI Workshop] Failed to download addon " .. workshopId)
                if callback then callback(false, nil) end
                return
            end
            
            print("[AI Workshop] Downloaded to: " .. path)
            
            -- Mount the addon
            local success, files = game.MountGMA(path)
            if success then
                print("[AI Workshop] Successfully mounted addon " .. workshopId .. " (" .. #files .. " files)")
                if callback then callback(true, path) end
            else
                print("[AI Workshop] Failed to mount addon " .. workshopId)
                if callback then callback(false, path) end
            end
        end)
    else
        -- SERVER: Request client to download, then wait for response
        _AI_PENDING_DOWNLOADS[workshopId] = callback
        
        net.Start("AI_RequestWorkshopDownload")
        net.WriteString(workshopId)
        net.Send(Entity(1)) -- Send to player 1
    end
end

-- Download addon and get list of assets organized by category
-- callback(assets) where assets = {models={}, materials={}, sounds={}, weapons={}, entities={}} or nil on failure
function DownloadAndGetWorkshopAssets(workshopId, callback)
    DownloadAndMountWorkshopAddon(workshopId, function(success, data)
        if not success then
            if callback then callback(nil) end
            return
        end
        
        -- On server, data already contains complete assets from client
        if SERVER and data and data.models then
            local assets = {
                models = data.models or {},
                materials = data.materials or {},
                sounds = data.sounds or {},
                weapons = data.weapons or {},
                entities = data.entities or {},
                npcs = data.npcs or {}
            }
            print("[AI Workshop] Server received for addon " .. workshopId .. ": " .. #assets.models .. " models, " .. #assets.weapons .. " weapons, " .. #assets.entities .. " entities, " .. #assets.npcs .. " NPCs")
            if callback then callback(assets) end
            return
        end
        
        -- On client, scan for assets locally
        local assets = {
            models = {},
            materials = {},
            sounds = {},
            weapons = {},
            entities = {},
            npcs = {}
        }
        
        -- Recursive model search
        local function SearchModels(basePath)
            local files, dirs = file.Find(basePath .. "/*", "GAME")
            for _, f in ipairs(files or {}) do
                if string.EndsWith(f, ".mdl") then
                    table.insert(assets.models, basePath .. "/" .. f)
                end
            end
            for _, d in ipairs(dirs or {}) do
                SearchModels(basePath .. "/" .. d)
            end
        end
        SearchModels("models")
        
        -- Small delay to ensure addon files are fully accessible
        timer.Simple(0.1, function()
            -- Scan for weapon and entity Lua files
            print("[AI Workshop] Scanning for weapons in lua/weapons/*.lua")
            local weaponFiles, _ = file.Find("lua/weapons/*.lua", "GAME")
            print("[AI Workshop] Found " .. (#weaponFiles or 0) .. " weapon files")
            for _, f in ipairs(weaponFiles or {}) do
                local weaponName = string.StripExtension(f)
                print("[AI Workshop] Found weapon file: " .. f .. " -> class: " .. weaponName)
                table.insert(assets.weapons, weaponName)
            end
            
            print("[AI Workshop] Scanning for entities in lua/entities/*")
            local entityDirs, _ = file.Find("lua/entities/*", "GAME")
            print("[AI Workshop] Found " .. (#entityDirs or 0) .. " entity entries")
            for _, dir in ipairs(entityDirs or {}) do
                -- Check if it's a directory with init.lua or a .lua file
                if file.Exists("lua/entities/" .. dir .. "/init.lua", "GAME") then
                    print("[AI Workshop] Found entity directory: " .. dir)
                    table.insert(assets.entities, dir)
                elseif string.EndsWith(dir, ".lua") then
                    local entityName = string.StripExtension(dir)
                    print("[AI Workshop] Found entity file: " .. dir .. " -> class: " .. entityName)
                    table.insert(assets.entities, entityName)
                end
            end
            
            print("[AI Workshop] Found in addon " .. workshopId .. ": " .. #assets.models .. " models, " .. #assets.weapons .. " weapons, " .. #assets.entities .. " entities")
            
            -- Send complete data to server (including weapons and entities)
            if CLIENT then
                net.Start("AI_WorkshopDownloadComplete")
                net.WriteString(workshopId)
                net.WriteString(data or "")
                net.WriteTable(assets)
                net.SendToServer()
            end
            
            if callback then callback(assets) end
        end)
    end)
end

-- Download addon and get the best model path (without spawning)
-- callback(modelPath) - called with the selected model path (or nil on failure)
-- Model is automatically precached before callback
function DownloadAndGetWorkshopModel(workshopId, callback)
    DownloadAndGetWorkshopAssets(workshopId, function(assets)
        if not assets or #assets.models == 0 then
            print("[AI Workshop] No models found in addon " .. tostring(workshopId))
            if callback then callback(nil) end
            return
        end
        
        -- Find first valid spawnable model
        local modelToSpawn = nil
        for _, model in ipairs(assets.models) do
            if IsValidSpawnableModel(model) then
                modelToSpawn = model
                break
            end
        end
        
        if not modelToSpawn then
            modelToSpawn = assets.models[1] -- Fallback to first model
        end
        
        print("[AI Workshop] Selected model: " .. modelToSpawn)
        
        -- Precache the model before returning it
        if SERVER then
            util.PrecacheModel(modelToSpawn)
            print("[AI Workshop] Precached model: " .. modelToSpawn)
            
            -- Small delay to ensure model is fully loaded before callback
            timer.Simple(0.1, function()
                if callback then callback(modelToSpawn) end
            end)
        else
            -- Client doesn't need delay
            if callback then callback(modelToSpawn) end
        end
    end)
end

-- Download addon and spawn the first valid model found
-- callback(entity) - called with the spawned entity (or nil on failure)
function DownloadAndSpawnWorkshopModel(workshopId, callback)
    DownloadAndGetWorkshopModel(workshopId, function(modelPath)
        if not modelPath then
            if callback then callback(nil) end
            return
        end
        
        print("[AI Workshop] Spawning model: " .. modelPath)
        
        -- Determine entity type based on model path
        -- Default to ragdoll for character models, prop_physics for objects
        local entityClass = "prop_ragdoll"
        local modelLower = string.lower(modelPath)
        
        -- Only use prop_physics if it's clearly a prop (not a character)
        if string.find(modelLower, "props/") or 
           string.find(modelLower, "props_") or
           string.find(modelLower, "vehicle") or
           string.find(modelLower, "weapons") then
            entityClass = "prop_physics"
        end
        
        -- Spawn in front of player
        local ply = Entity(1)
        if not IsValid(ply) then
            print("[AI Workshop] Invalid player entity")
            if callback then callback(nil) end
            return
        end
        
        local spawnPos = ply:GetPos() + ply:GetForward() * 100 + Vector(0, 0, 50)
        
        local ent = ents.Create(entityClass)
        ent:SetModel(modelPath)
        ent:SetPos(spawnPos)
        ent:Spawn()
        ent:Activate()
        
        -- Wake physics (check if it returns a valid object first)
        local phys = ent:GetPhysicsObject()
        if phys and phys:IsValid() then
            phys:Wake()
        end
        
        print("[AI Workshop] Spawned " .. entityClass .. ": " .. modelPath)
        if callback then callback(ent) end
    end)
end

-- Smart download and spawn - detects addon type and spawns appropriately
-- callback(result) where result = {type="weapon|entity|model", object=<entity or weapon name>} or nil
function DownloadAndSpawn(workshopId, callback)
    print("[AI Workshop] Smart-spawning addon: " .. workshopId)
    
    DownloadAndGetWorkshopAssets(workshopId, function(assets)
        if not assets then
            print("[AI Workshop] Failed to download addon " .. workshopId)
            if callback then callback(nil) end
            return
        end
        
        local ply = Entity(1)
        if not IsValid(ply) then
            print("[AI Workshop] Invalid player entity")
            if callback then callback(nil) end
            return
        end
        
        -- Priority 1: Weapons (give to player)
        if #assets.weapons > 0 then
            local weaponClass = assets.weapons[1]
            print("[AI Workshop] Detected weapon addon, giving weapon: " .. weaponClass)
            
            -- Small delay to ensure weapon is fully registered after mount
            timer.Simple(0.2, function()
                if IsValid(ply) then
                    local weapon = ply:Give(weaponClass)
                    if IsValid(weapon) then
                        print("[AI Workshop] Successfully gave weapon: " .. weaponClass)
                        ply:SelectWeapon(weaponClass)
                    else
                        print("[AI Workshop] Failed to give weapon: " .. weaponClass .. " (weapon may not exist or name may be incorrect)")
                    end
                end
            end)
            
            if callback then 
                callback({
                    type = "weapon",
                    name = weaponClass
                })
            end
            return
        end
        
        -- Priority 2: Entities (spawn in front of player)
        if #assets.entities > 0 then
            print("[AI Workshop] Detected entity addon, spawning entity: " .. assets.entities[1])
            local spawnPos = ply:GetPos() + ply:GetForward() * 100 + Vector(0, 0, 50)
            local ent = ents.Create(assets.entities[1])
            ent:SetPos(spawnPos)
            ent:Spawn()
            ent:Activate()
            
            if callback then
                callback({
                    type = "entity",
                    object = ent,
                    name = assets.entities[1]
                })
            end
            return
        end
        
        -- Priority 3: NPCs registered via list.Set (from autorun files)
        if #assets.npcs > 0 then
            local npcInfo = assets.npcs[1]
            print("[AI Workshop] Detected NPC addon, spawning NPC: " .. npcInfo.class)
            local spawnPos = ply:GetPos() + ply:GetForward() * 100 + Vector(0, 0, 50)
            local npcData = list.Get("NPC")[npcInfo.class]
            if npcData then
                local npc = ents.Create(npcData.Class)
                npc:SetPos(spawnPos)
                if npcData.Model then npc:SetModel(npcData.Model) end
                if npcData.KeyValues then
                    for k, v in pairs(npcData.KeyValues) do
                        npc:SetKeyValue(k, v)
                    end
                end
                npc:Spawn()
                npc:Activate()
                
                if callback then
                    callback({
                        type = "npc",
                        object = npc,
                        name = assets.npcs[1]
                    })
                end
                return
            end
        end
        
        -- Priority 4: Models (spawn as prop)
        if #assets.models > 0 then
            print("[AI Workshop] Detected model addon, spawning model")
            
            -- Find first valid spawnable model
            local modelToSpawn = nil
            for _, model in ipairs(assets.models) do
                if IsValidSpawnableModel(model) then
                    modelToSpawn = model
                    break
                end
            end
            
            if not modelToSpawn then
                modelToSpawn = assets.models[1] -- Fallback to first model
            end
            
            print("[AI Workshop] Selected model: " .. modelToSpawn)
            
            -- Spawn the model
            local entityClass = "prop_physics"
            if string.find(string.lower(modelToSpawn), "ragdoll") then
                entityClass = "prop_ragdoll"
            end
            
            local spawnPos = ply:GetPos() + ply:GetForward() * 100 + Vector(0, 0, 50)
            local ent = ents.Create(entityClass)
            ent:SetModel(modelToSpawn)
            ent:SetPos(spawnPos)
            ent:Spawn()
            ent:Activate()
            
            local phys = ent:GetPhysicsObject()
            if phys and phys:IsValid() then
                phys:Wake()
            end
            
            print("[AI Workshop] Spawned " .. entityClass .. ": " .. modelToSpawn)
            
            if callback then
                callback({
                    type = "model",
                    object = ent
                })
            end
            return
        end
        
        print("[AI Workshop] No spawnnable content found in addon " .. workshopId)
        if callback then callback(nil) end
    end)
end

-- Get all assets from all currently mounted addons
-- Returns {models={}, materials={}, sounds={}} for known addon content
function GetAllMountedAddonAssets()
    local assets = {
        models = {},
        materials = {},
        sounds = {}
    }
    
    -- Search through common model directories
    local function SearchDir(basePath, extension, targetTable)
        local files, dirs = file.Find(basePath .. "/*", "GAME")
        for _, f in ipairs(files or {}) do
            if string.EndsWith(f, extension) then
                table.insert(targetTable, basePath .. "/" .. f)
            end
        end
        for _, d in ipairs(dirs or {}) do
            SearchDir(basePath .. "/" .. d, extension, targetTable)
        end
    end
    
    -- Limit search to avoid performance issues
    local searchPaths = {"models/props_workshop", "models/player/workshop", "models/workshop"}
    for _, path in ipairs(searchPaths) do
        SearchDir(path, ".mdl", assets.models)
    end
    
    return assets
end

-- ============================================================================
-- NETWORKING HANDLERS
-- ============================================================================

if CLIENT then
    -- CLIENT: Receive download request from server
    -- CLIENT: Handle download requests from server
    net.Receive("AI_RequestWorkshopDownload", function()
        local workshopId = net.ReadString()
        print("[AI Workshop] Client received download request: " .. workshopId)
        
        -- Download on client
        steamworks.DownloadUGC(workshopId, function(gmaPath, file)
            if not gmaPath then
                print("[AI Workshop] Client failed to download: " .. workshopId)
                return
            end
            
            -- Mount the addon
            local mounted, fileList = game.MountGMA(gmaPath)
            if not mounted then
                print("[AI Workshop] Client failed to mount: " .. workshopId)
                return
            end
            
            print("[AI Workshop] Client mounted addon: " .. workshopId .. " (" .. #fileList .. " files)")
            
            -- Scan for assets from fileList (game.MountGMA returns complete file list)
            local assets = {
                models = {},
                materials = {},
                sounds = {},
                weapons = {},
                entities = {}
            }
            
            print("[AI Workshop] Scanning " .. #fileList .. " files from addon...")
            
            -- Scan through all files in the addon
            for _, filePath in ipairs(fileList) do
                local lowerPath = string.lower(filePath)
                
                -- Models
                if string.EndsWith(lowerPath, ".mdl") then
                    table.insert(assets.models, filePath)
                
                -- Weapons - two formats:
                -- 1. lua/weapons/<name>/shared.lua -> class is <name>
                -- 2. lua/weapons/<name>.lua -> class is <name>
                elseif string.match(lowerPath, "^lua/weapons/([^/]+)/") then
                    local weaponName = string.match(filePath, "lua/weapons/([^/]+)/")
                    if weaponName and not table.HasValue(assets.weapons, weaponName) then
                        print("[AI Workshop] Found weapon: " .. weaponName)
                        table.insert(assets.weapons, weaponName)
                    end
                elseif string.match(lowerPath, "^lua/weapons/(.+)%.lua$") then
                    local weaponName = string.match(filePath, "lua/weapons/(.+)%.lua")
                    if weaponName and not table.HasValue(assets.weapons, weaponName) then
                        print("[AI Workshop] Found weapon: " .. weaponName)
                        table.insert(assets.weapons, weaponName)
                    end
                
                -- Entities (lua/entities/*/init.lua or lua/entities/*.lua)
                elseif string.match(lowerPath, "^lua/entities/(.+)/init%.lua$") then
                    local entityName = string.match(filePath, "lua/entities/(.+)/init%.lua")
                    if entityName and not table.HasValue(assets.entities, entityName) then
                        print("[AI Workshop] Found entity: " .. entityName)
                        table.insert(assets.entities, entityName)
                    end
                elseif string.match(lowerPath, "^lua/entities/(.+)%.lua$") then
                    local entityName = string.match(filePath, "lua/entities/(.+)%.lua")
                    if entityName and not table.HasValue(assets.entities, entityName) then
                        print("[AI Workshop] Found entity: " .. entityName)
                        table.insert(assets.entities, entityName)
                    end
                end
            end
            
            print("[AI Workshop] Client found: " .. #assets.models .. " models, " .. #assets.weapons .. " weapons, " .. #assets.entities .. " entities")
            
            -- NOTE: Cannot execute autorun files from dynamically mounted GMAs on CLIENT
            -- file.Open() doesn't work with mounted content, so file.Read() fails
            -- NPCs registered via autorun won't be detected - use DownloadAndGetWorkshopModel + manual NPC spawn instead
            
            -- Send complete data to server
            net.Start("AI_WorkshopDownloadComplete")
            net.WriteString(workshopId)
            net.WriteString(gmaPath)
            net.WriteTable(assets)
            net.SendToServer()
        end)
    end)
    
    -- CLIENT: Receive autorun code from server and execute it
    net.Receive("AI_RunAutorunOnClient", function()
        local workshopId = net.ReadString()
        local filePath = net.ReadString()
        local code = net.ReadString()
        local addonModels = net.ReadTable()
        print("[AI Workshop] Client executing autorun: " .. filePath .. " (" .. string.len(code) .. " bytes)")
        
        -- Compare against baseline instead of current state
        local npcsBefore = _AI_BASELINE_STATE.npcs
        
        local success, err = pcall(RunString, code, filePath)
        if success then
            print("[AI Workshop] Client executed: " .. filePath)
            
            -- Check immediately
            local npcsImmediate = list.Get("NPC")
            local countImmediate = table.Count(npcsImmediate)
            
            local function collectNewNPCs(npcsNow, npcsPrevious)
                local detected = {}
                for className, npcData in pairs(npcsNow) do
                    if not npcsPrevious[className] and npcData.Model then
                        -- Only include NPCs whose models match addon models
                        local isFromAddon = false
                        for _, addonModel in ipairs(addonModels) do
                            if npcData.Model == addonModel then
                                isFromAddon = true
                                break
                            end
                        end
                        
                        if isFromAddon then
                            print("[AI Workshop] New NPC: " .. className .. " (model=" .. tostring(npcData.Model) .. ")")
                            table.insert(detected, {
                                class = className,
                                model = npcData.Model,
                                name = npcData.Name or className
                            })
                        end
                    end
                end
                return detected
            end
            
            local detectedNPCs = collectNewNPCs(npcsImmediate, npcsBefore)
            
            if #detectedNPCs > 0 then
                print("[AI Workshop] Client detected " .. #detectedNPCs .. " NPCs immediately!")
                net.Start("AI_ClientDetectedNPCs")
                net.WriteString(workshopId)
                net.WriteTable(detectedNPCs)
                net.SendToServer()
            else
                -- Check after delay (some autorun files register NPCs in timers)
                timer.Simple(0.5, function()
                    local npcsDelayed = list.Get("NPC")
                    local detectedDelayed = collectNewNPCs(npcsDelayed, npcsBefore)
                    
                    if #detectedDelayed > 0 then
                        print("[AI Workshop] Client detected " .. #detectedDelayed .. " NPCs after delay!")
                        net.Start("AI_ClientDetectedNPCs")
                        net.WriteString(workshopId)
                        net.WriteTable(detectedDelayed)
                        net.SendToServer()
                    else
                        print("[AI Workshop] No new NPCs detected (possibly already registered from previous load)")
                    end
                end)
            end
        else
            print("[AI Workshop] Client execution error: " .. tostring(err))
        end
    end)
end

if SERVER then
    -- SERVER: Receive download complete notification from client
    net.Receive("AI_WorkshopDownloadComplete", function(len, ply)
        local workshopId = net.ReadString()
        local gmaPath = net.ReadString()
        local assets = net.ReadTable()
        
        print("[AI Workshop] Server received download complete: " .. workshopId .. " (path=" .. gmaPath .. ", models=" .. (assets.models and #assets.models or 0) .. ", weapons=" .. (assets.weapons and #assets.weapons or 0) .. ", entities=" .. (assets.entities and #assets.entities or 0) .. ", npcs=" .. (assets.npcs and #assets.npcs or 0) .. ")")
        
        -- SERVER: Also mount the addon so entities spawn with proper physics
        if gmaPath ~= "" then
            local mounted, files = game.MountGMA(gmaPath)
            if mounted then
                print("[AI Workshop] Server also mounted addon: " .. workshopId)
                
                -- Try reading autorun files on SERVER (like VLL2 does)
                if files then
                    -- Compare against baseline instead of current state
                    local npcsBefore = _AI_BASELINE_STATE.npcs
                    
                    for _, filePath in ipairs(files) do
                        if string.StartWith(filePath, "lua/autorun/") and string.EndsWith(filePath, ".lua") then
                            print("[AI Workshop] Server found autorun file: " .. filePath)
                            local success, content = pcall(file.Read, filePath, "GAME")
                            if success and content then
                                print("[AI Workshop] Server successfully read: " .. filePath .. " (" .. string.len(content) .. " bytes)")
                                -- Execute on SERVER
                                local execSuccess, err = pcall(RunString, content, filePath)
                                if execSuccess then
                                    print("[AI Workshop] Server executed: " .. filePath)
                                else
                                    print("[AI Workshop] Server execution error: " .. tostring(err))
                                end
                                
                                -- Send to CLIENT to execute there too
                                net.Start("AI_RunAutorunOnClient")
                                net.WriteString(workshopId)
                                net.WriteString(filePath)
                                net.WriteString(content)
                                net.WriteTable(assets.models or {})
                                net.Broadcast()
                            else
                                print("[AI Workshop] Server file.Read failed: " .. tostring(content))
                            end
                        end
                    end
                    
                    -- Check if new NPCs were registered (compared to baseline)
                    -- Only include NPCs whose models are from this addon
                    local npcsAfter = list.Get("NPC")
                    local newNPCs = {}
                    for className, npcData in pairs(npcsAfter) do
                        if not npcsBefore[className] and npcData.Model then
                            -- Check if this NPC's model is from the workshop addon
                            local isFromAddon = false
                            for _, addonModel in ipairs(assets.models) do
                                if npcData.Model == addonModel then
                                    isFromAddon = true
                                    break
                                end
                            end
                            
                            if isFromAddon then
                                print("[AI Workshop] New NPC: " .. className .. " (model=" .. tostring(npcData.Model) .. ")")
                                table.insert(newNPCs, {
                                    class = className,
                                    model = npcData.Model,
                                    name = npcData.Name
                                })
                            end
                        end
                    end
                    
                    if #newNPCs > 0 then
                        print("[AI Workshop] Server detected " .. #newNPCs .. " new NPCs registered!")
                        if not assets.npcs then assets.npcs = {} end
                        for _, npcData in ipairs(newNPCs) do
                            table.insert(assets.npcs, npcData)
                        end
                    end
                end
            else
                print("[AI Workshop] Server failed to mount addon: " .. workshopId)
            end
        end
        
        -- Call pending callback
        local callback = _AI_PENDING_DOWNLOADS[workshopId]
        if callback then
            callback(true, assets)
            _AI_PENDING_DOWNLOADS[workshopId] = nil
        end
    end)
    
    -- SERVER: Receive NPCs detected by CLIENT after autorun execution
    net.Receive("AI_ClientDetectedNPCs", function(len, ply)
        local workshopId = net.ReadString()
        local detectedNPCs = net.ReadTable()
        
        print("[AI Workshop] Server received " .. #detectedNPCs .. " NPCs from CLIENT for addon: " .. workshopId)
        for _, npcData in ipairs(detectedNPCs) do
            print("[AI Workshop] Client-detected NPC: " .. npcData.class .. " (model=" .. tostring(npcData.model) .. ")")
        end
        
        -- Note: These NPCs are only registered on CLIENT, so SERVER can't spawn them directly
        -- They will appear in spawn menu but won't be spawnable via our smart spawn function
    end)
end

print("[AI Chaos] Workshop helpers loaded")
