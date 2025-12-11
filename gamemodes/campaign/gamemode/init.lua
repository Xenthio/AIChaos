-- Campaign Gamemode - Server Init
AddCSLuaFile("cl_init.lua")
AddCSLuaFile("shared.lua")
AddCSLuaFile("player_class.lua")

include("shared.lua")

-- ConVar for load last save on death (off by default)
local cv_loadlastsave = CreateConVar( "campaign_load_last_save", "0", FCVAR_ARCHIVE + FCVAR_REPLICATED, "Load last save on death instead of respawning" )

function GM:Initialize()
	print("[Campaign] Campaign Gamemode Initialized")
	
	-- Set loading screen to our custom HTML that shows the last frame
	RunConsoleCommand("sv_loadingurl", "asset://garrysmod/addons/aichaos/gamemodes/campaign/gamemode/html/loading.html")
	
	-- HL2 Game Rules
	RunConsoleCommand("gmod_suit", "1")
	RunConsoleCommand("gmod_maxammo", "0") -- use per-weapon max ammo
	RunConsoleCommand("sv_defaultdeployspeed", "1")
	RunConsoleCommand("mp_falldamage", "1") -- 0 = MP style (10 dmg), 1 = Realistic HL2 Style (Velocity based)
	
	-- HL2 Movement Physics
	--RunConsoleCommand("sv_accelerate", "10")
	--RunConsoleCommand("sv_airaccelerate", "10")
	RunConsoleCommand("sv_friction", "4")
	RunConsoleCommand("sv_stopspeed", "100")
	RunConsoleCommand("sv_sticktoground", "0")
	RunConsoleCommand("sv_maxspeed", "320")
	
	-- Disable Sandbox cheats/features
	RunConsoleCommand("sbox_noclip", "0")
	RunConsoleCommand("sbox_godmode", "0")
end

-- Prevent weapons from dropping on death
function GM:ShouldDropWeapon( ply, wep )
	return false
end

-- Force it with a hook just in case
hook.Add( "ShouldDropWeapon", "CampaignNoDrop", function( ply, wep )
	return false
end )

-- Disable ragdoll creation on death (HL2 SP style)
function GM:CreateEntityRagdoll( ent, ragdoll )
	if ent:IsPlayer() then
		if IsValid( ragdoll ) then
			ragdoll:Remove()
		end
		return true
	end
end

-- Prevent ragdoll in DoPlayerDeath
function GM:DoPlayerDeath( ply, attacker, dmginfo )
	ply:CreateRagdoll() -- This creates the ragdoll but we remove it below
	ply:AddDeaths( 1 )
	
	if IsValid( attacker ) and attacker:IsPlayer() then
		if attacker == ply then
			attacker:AddFrags( -1 )
		else
			attacker:AddFrags( 1 )
		end
	end
end

-- Remove ragdoll after it's created
hook.Add( "PlayerDeath", "RemoveDeathRagdoll", function( ply )
	timer.Simple( 0, function()
		if IsValid( ply ) then
			local ragdoll = ply:GetRagdollEntity()
			if IsValid( ragdoll ) then
				ragdoll:Remove()
			end
		end
	end )
end )

-- Get the most recent save file (like HL2 does)
local function GetMostRecentSave()
	local saves = { "quick", "autosave", "autosavedangerous" }
	local newest = nil
	local newestTime = 0
	
	for _, save in ipairs( saves ) do
		local path = "save/" .. save .. ".sav"
		if file.Exists( path, "GAME" ) then
			local time = file.Time( path, "GAME" )
			if time > newestTime then
				newestTime = time
				newest = save
			end
		end
	end
	
	return newest or "quick"
end

-- Network strings
util.AddNetworkString( "CampaignLoadSaveHint" )
util.AddNetworkString( "CampaignLevelChange" )

-- Player death think - handle respawn or load last save
function GM:PlayerDeathThink( ply )
	--if ply.NextSpawnTime and ply.NextSpawnTime > CurTime() then return end
	
	if ply:IsBot() or ply:KeyPressed( IN_ATTACK ) or ply:KeyPressed( IN_ATTACK2 ) or ply:KeyPressed( IN_JUMP ) then
		if cv_loadlastsave:GetBool() then
			-- Try to load save using pre-defined aliases from autoexec.cfg
			-- User must add these to autoexec.cfg:
			--   alias _load_quick "load quick"
			--   alias _load_autosave "load autosave"
			--   alias _load_autosavedangerous "load autosavedangerous"
			if not false then --ply.TriedLoad then
				local saveToLoad = GetMostRecentSave()
				
				-- Run the alias (defined outside Lua to bypass command blocking)
				game.ConsoleCommand( "_load_" .. saveToLoad .. "\n" )
				
				-- Also send hint in case alias isn't set up
				net.Start( "CampaignLoadSaveHint" )
					net.WriteString( saveToLoad )
				net.Send( ply )
				ply.TriedLoad = true
			end
		else
			ply:Spawn()
		end
	end
end

-- Save weapons and ammo on death
function GM:PlayerDeath( ply, inflictor, attacker )
	ply.TriedLoad = false -- Reset so load can be attempted again
	ply.SavedWeapons = {}
	for _, wep in ipairs( ply:GetWeapons() ) do
		ply.SavedWeapons[wep:GetClass()] = {
			clip1 = wep:Clip1(),
			clip2 = wep:Clip2()
		}
	end
	
	ply.SavedAmmo = ply:GetAmmo()
	
	self.BaseClass:PlayerDeath( ply, inflictor, attacker )
end

-- Restore weapons on spawn
function GM:PlayerLoadout( ply )
	if ply.SavedWeapons then
		for class, data in pairs( ply.SavedWeapons ) do
			local wep = ply:Give( class )
			if IsValid(wep) then
				wep:SetClip1( data.clip1 )
				wep:SetClip2( data.clip2 )
			end
		end
		
		for id, count in pairs( ply.SavedAmmo ) do
			ply:SetAmmo( count, id )
		end
		
		ply.SavedWeapons = nil
		ply.SavedAmmo = nil
		return true
	end
	
	return true
end

-- Save velocity on level change (server shutdown/map change)
function GM:ShutDown()
	-- Notify clients to capture loading frame
	net.Start( "CampaignLevelChange" )
	net.Broadcast()
	
	for _, ply in ipairs( player.GetAll() ) do
		local vel = ply:GetVelocity()
		ply:SetPData( "campaign_velocity_x", vel.x )
		ply:SetPData( "campaign_velocity_y", vel.y )
		ply:SetPData( "campaign_velocity_z", vel.z )
		ply:SetPData( "campaign_transitioning", "1" )
	end
end

function GM:PlayerInitialSpawn( ply )
	self.BaseClass:PlayerInitialSpawn( ply )
	
	-- Check if we are transitioning from another level
	if ply:GetPData( "campaign_transitioning" ) == "1" then
		local x = tonumber( ply:GetPData( "campaign_velocity_x" ) )
		local y = tonumber( ply:GetPData( "campaign_velocity_y" ) )
		local z = tonumber( ply:GetPData( "campaign_velocity_z" ) )
		
		ply:SetPData( "campaign_transitioning", "0" )
		
		ply.RestoreVelocity = Vector( x, y, z )
	end
end

-- Prevent strange drowning sounds on spawn
hook.Add( "EntityEmitSound", "BlockSpawnDrowning", function( data )
	if ( IsValid( data.Entity ) and data.Entity:IsPlayer() and data.Entity.IsSpawning ) then
		local soundName = data.SoundName:lower()
		if ( soundName:find( "drown" ) or soundName:find( "water" ) ) then
			return false
		end
	end
end )

-- Player spawn - only remove suit if they haven't picked it up yet
function GM:PlayerSpawn(ply)
	ply.IsSpawning = true
	timer.Simple( 1, function()
		if ( IsValid( ply ) ) then ply.IsSpawning = false end
	end )

	-- Only cleanup on respawn (not initial spawn)
	if ply.HasSpawned then
		--RunConsoleCommand("gmod_admin_cleanup")
	else
		ply.HasSpawned = true
	end

	player_manager.SetPlayerClass( ply, "player_campaign" )
	
	self.BaseClass:PlayerSpawn(ply)

	if ply.RestoreVelocity then
		-- Apply velocity after a short delay to ensure physics are ready
		local vel = ply.RestoreVelocity
		timer.Simple(0.1, function()
			if IsValid(ply) then
				ply:SetVelocity( vel )
			end
		end)
		ply.RestoreVelocity = nil
	end
	
	-- -- Only remove suit if player hasn't picked it up yet
	-- if ply:GetPData("campaign_has_suit", "0") ~= "1" then
	-- 	ply:RemoveSuit()
	-- end
	
	-- -- Check suit status shortly after spawn and save it
	-- timer.Simple(0.5, function()
	-- 	if IsValid(ply) and ply:IsSuitEquipped() then
	-- 		ply:SetPData("campaign_has_suit", "1")
	-- 	end
	-- end)
end

-- -- Save suit status when player disconnects or changes level
-- function GM:PlayerDisconnected(ply)
-- 	if ply:IsSuitEquipped() then
-- 		ply:SetPData("campaign_has_suit", "1")
-- 	end
-- end

-- -- Also save on death (in case of level transition)
-- function GM:PostPlayerDeath(ply)
-- 	if ply:IsSuitEquipped() then
-- 		ply:SetPData("campaign_has_suit", "1")
-- 	end
-- end
