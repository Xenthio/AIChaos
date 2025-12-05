-- Campaign Gamemode - Server Init
AddCSLuaFile("cl_init.lua")
AddCSLuaFile("shared.lua")
AddCSLuaFile("player_class.lua")

include("shared.lua")

function GM:Initialize()
	print("[Campaign] Campaign Gamemode Initialized")
	
	-- HL2 Game Rules
	RunConsoleCommand("gmod_suit", "1")
	RunConsoleCommand("sv_defaultdeployspeed", "1")
	RunConsoleCommand("mp_falldamage", "1") -- 0 = MP style (10 dmg), 1 = Realistic HL2 Style (Velocity based)
	
	-- HL2 Movement Physics
	--RunConsoleCommand("sv_accelerate", "10")
	--RunConsoleCommand("sv_airaccelerate", "10")
	RunConsoleCommand("sv_friction", "4")
	RunConsoleCommand("sv_stopspeed", "100")
	RunConsoleCommand("sv_sticktoground", "0")
	
	-- Disable Sandbox cheats/features
	RunConsoleCommand("sbox_noclip", "0")
	RunConsoleCommand("sbox_godmode", "0")
end

-- Prevent weapons from dropping on death
function GM:ShouldDropWeapon( ply, wep )
	return false
end

-- Save velocity on level change (server shutdown/map change)
function GM:ShutDown()
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
