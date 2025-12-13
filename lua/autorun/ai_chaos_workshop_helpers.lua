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
end

-- Table to track pending workshop downloads
_AI_PENDING_DOWNLOADS = _AI_PENDING_DOWNLOADS or {}

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
-- callback(success, assets) where assets = {models={}, materials={}, sounds={}}
function DownloadAndGetWorkshopAssets(workshopId, callback)
    DownloadAndMountWorkshopAddon(workshopId, function(success, data)
        if not success then
            if callback then callback(false, nil) end
            return
        end
        
        -- On server, data already contains models list from client
        if SERVER and data and data.models then
            local assets = {
                models = data.models,
                materials = {},
                sounds = {}
            }
            print("[AI Workshop] Server received " .. #assets.models .. " models for addon " .. workshopId)
            if callback then callback(true, assets) end
            return
        end
        
        -- On client, scan for assets locally
        local assets = {
            models = {},
            materials = {},
            sounds = {}
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
        
        print("[AI Workshop] Found " .. #assets.models .. " models in addon " .. workshopId)
        if callback then callback(true, assets) end
    end)
end

-- Download addon and get the best model path (without spawning)
-- callback(modelPath) - called with the selected model path (or nil on failure)
-- Model is automatically precached before callback
function DownloadAndGetWorkshopModel(workshopId, callback)
    DownloadAndGetWorkshopAssets(workshopId, function(success, assets)
        if not success or not assets or #assets.models == 0 then
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
    net.Receive("AI_RequestWorkshopDownload", function()
        local workshopId = net.ReadString()
        print("[AI Workshop] Client received download request: " .. workshopId)
        
        -- Download on client
        steamworks.DownloadUGC(workshopId, function(path, file)
            local success = false
            local modelsList = {}
            
            if path then
                -- Mount the addon
                local mounted, files = game.MountGMA(path)
                if mounted then
                    print("[AI Workshop] Client mounted addon: " .. workshopId .. " (" .. #files .. " files)")
                    success = true
                    
                    -- game.MountGMA returns the list of files, use that instead of file.Find
                    for _, filePath in ipairs(files) do
                        if string.EndsWith(string.lower(filePath), ".mdl") then
                            table.insert(modelsList, filePath)
                        end
                    end
                    
                    print("[AI Workshop] Client found " .. #modelsList .. " models")
                end
            end
            
            -- Tell server we're done (include path so server can mount too)
            net.Start("AI_WorkshopDownloadComplete")
            net.WriteString(workshopId)
            net.WriteBool(success)
            net.WriteString(path or "")
            net.WriteUInt(#modelsList, 16)
            for _, model in ipairs(modelsList) do
                net.WriteString(model)
            end
            net.SendToServer()
        end)
    end)
end

if SERVER then
    -- SERVER: Receive download complete notification from client
    net.Receive("AI_WorkshopDownloadComplete", function(len, ply)
        local workshopId = net.ReadString()
        local success = net.ReadBool()
        local gmaPath = net.ReadString()
        local modelCount = net.ReadUInt(16)
        local models = {}
        
        for i = 1, modelCount do
            table.insert(models, net.ReadString())
        end
        
        print("[AI Workshop] Server received download complete: " .. workshopId .. " (path=" .. gmaPath .. ", models=" .. modelCount .. ")")
        
        -- SERVER: Also mount the addon so entities spawn with proper physics
        if success and gmaPath ~= "" then
            local mounted, files = game.MountGMA(gmaPath)
            if mounted then
                print("[AI Workshop] Server also mounted addon: " .. workshopId)
            else
                print("[AI Workshop] Server failed to mount addon: " .. workshopId)
            end
        end
        
        -- Call pending callback
        local callback = _AI_PENDING_DOWNLOADS[workshopId]
        if callback then
            if success then
                callback(true, {models = models})
            else
                callback(false, nil)
            end
            _AI_PENDING_DOWNLOADS[workshopId] = nil
        end
    end)
end

print("[AI Chaos] Workshop helpers loaded")
