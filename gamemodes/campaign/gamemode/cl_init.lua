-- Campaign Gamemode - Client Init
include("shared.lua")

function GM:Initialize()
	print("[Campaign] Client Initialized")
end

--
-- Load save hint (GMod blocks 'load' command from Lua, so we show a hint)
--
local LoadSaveHint = nil
local LoadSaveTime = 0

net.Receive( "CampaignLoadSaveHint", function()
	local saveName = net.ReadString()
	LoadSaveHint = saveName
	LoadSaveTime = CurTime()
end )

hook.Add( "HUDPaint", "CampaignLoadSaveHint", function()
	if not LoadSaveHint then return end
	
	local ply = LocalPlayer()
	if IsValid( ply ) and ply:Health() > 0 then
		LoadSaveHint = nil -- Player respawned somehow, clear hint
		return
	end
	
	-- Draw hint text
	local text1 = "PRESS F9 TO QUICKLOAD"
	local text2 = "or load save: " .. LoadSaveHint
	
	surface.SetFont( "DermaLarge" )
	local w1, h1 = surface.GetTextSize( text1 )
	local w2, h2 = surface.GetTextSize( text2 )
	
	local x = ScrW() / 2
	local y = ScrH() * 0.7
	
	-- Pulsing alpha for visibility
	local pulse = math.abs( math.sin( CurTime() * 2 ) )
	local alpha = 150 + pulse * 105
	
	-- Draw shadow
	surface.SetTextColor( 0, 0, 0, alpha )
	surface.SetTextPos( x - w1/2 + 2, y + 2 )
	surface.DrawText( text1 )
	surface.SetTextPos( x - w2/2 + 2, y + h1 + 10 + 2 )
	surface.DrawText( text2 )
	
	-- Draw text
	surface.SetTextColor( 255, 200, 80, alpha )
	surface.SetTextPos( x - w1/2, y )
	surface.DrawText( text1 )
	surface.SetTextColor( 200, 200, 200, alpha * 0.8 )
	surface.SetTextPos( x - w2/2, y + h1 + 10 )
	surface.DrawText( text2 )
end )

--
-- First-person death view (HL2 SP style)
--

-- Track death state locally to avoid network delay
local IsDead = false
local LastHealth = 100
local LastVelocityZ = 0
local DiedFromFall = false
local FallDeathTime = 0

hook.Add( "Think", "CampaignDeathCheck", function()
	local ply = LocalPlayer()
	if not IsValid( ply ) then return end
	
	local health = ply:Health()
	local velZ = ply:GetVelocity().z
	
	-- Detect death: health dropped to 0 or below
	if health <= 0 and LastHealth > 0 then
		IsDead = true
		
		-- Check if we died from fall damage (were falling fast before death)
		if LastVelocityZ < -600 then
			DiedFromFall = true
			FallDeathTime = CurTime()
		end
	end
	
	-- Detect respawn: health went from 0 to positive
	if health > 0 and IsDead then
		IsDead = false
		DiedFromFall = false
	end
	
	LastHealth = health
	LastVelocityZ = velZ
end )

-- Override CalcView to prevent the death chase cam
function GM:CalcView( ply, origin, angles, fov )
	local view = {}
	view.origin = origin
	view.angles = angles
	view.fov = fov
	view.drawviewer = false
	
	-- If dead, keep first person at player position, allow looking around
	if IsDead or ply:Health() <= 0 then
		view.origin = ply:GetPos() + Vector( 0, 0, 64 )
	end
	if not ply:Alive()  then
		view.origin = ply:GetPos() + Vector( 0, 0, 16 )
	end
	
	return view
end

-- Never draw local player in third person (prevents death cam showing player model)
function GM:ShouldDrawLocalPlayer( ply )
	return false
end

-- Hide viewmodel on death
function GM:PreDrawViewModel( vm, ply, weapon )
	if IsDead or not ply:Alive() or ply:Health() <= 0 then
		return true -- Don't draw
	end
end

-- Black screen on fall death
function GM:RenderScreenspaceEffects()
	if DiedFromFall then
		surface.SetDrawColor( 0, 0, 0, 255 )
		surface.DrawRect( -10, -10, ScrW() + 20, ScrH() + 20 )
	end
end
