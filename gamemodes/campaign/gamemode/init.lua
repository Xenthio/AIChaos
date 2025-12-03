-- Campaign Gamemode - Server Init
AddCSLuaFile("cl_init.lua")
AddCSLuaFile("shared.lua")

include("shared.lua")

function GM:Initialize()
	print("[Campaign] Campaign Gamemode Initialized")
end

-- Player spawn - remove HEV suit (you don't start with it in HL2)
function GM:PlayerSpawn(ply)
	self.BaseClass:PlayerSpawn(ply)
	
	-- Remove the suit
	ply:RemoveSuit()
end
