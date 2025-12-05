
DEFINE_BASECLASS( "player_default" )

local PLAYER = {}

PLAYER.DisplayName			= "Campaign Player"
PLAYER.WalkSpeed			= 190
PLAYER.RunSpeed				= 320 

function PLAYER:Spawn()
	BaseClass.Spawn( self )
	self.Player:EquipSuit()
	
	-- local ply = self.Player
	-- if ( ply:GetPData("campaign_has_suit", "0") == "1" ) then
	-- 	ply:EquipSuit()
	-- else
	-- 	ply:RemoveSuit()
	-- end
end

function PLAYER:Loadout()
	-- local ply = self.Player
	-- ply:Give( "weapon_crowbar" )
	-- ply:Give( "weapon_pistol" )
	-- ply:Give( "weapon_smg1" )
	-- ply:GiveAmmo( 255, "Pistol", true )
	-- ply:GiveAmmo( 255, "SMG1", true )
end

function PLAYER:SetModel()
	local cl_playermodel = self.Player:GetInfo( "cl_playermodel" )
	local modelname = player_manager.TranslatePlayerModel( cl_playermodel )
	util.PrecacheModel( modelname )
	self.Player:SetModel( modelname )
end



--
-- Reproduces the jump boost from HL2 singleplayer
--
local JUMPING

function PLAYER:StartMove( move )

	-- Only apply the jump boost in FinishMove if the player has jumped during this frame
	-- Using a global variable is safe here because nothing else happens between SetupMove and FinishMove
	if bit.band( move:GetButtons(), IN_JUMP ) ~= 0 and bit.band( move:GetOldButtons(), IN_JUMP ) == 0 and self.Player:OnGround() then
		JUMPING = true
	end

end

function PLAYER:FinishMove( move )

	-- If the player has jumped this frame
	if ( JUMPING ) then
        
		-- Get their orientation
		local forward = move:GetAngles()
		forward.p = 0
		forward = forward:Forward()

		-- Compute the speed boost

		-- HL2 normally provides a much weaker jump boost when sprinting
		-- For some reason this never applied to GMod, so we won't perform
		-- this check here to preserve the "authentic" feeling
		local speedBoostPerc = ( ( not move:KeyDown( IN_SPEED ) ) and ( not self.Player:Crouching() ) and 0.5 ) or 0.1

		local speedAddition = math.abs( move:GetForwardSpeed() * speedBoostPerc )
		local maxSpeed = move:GetMaxSpeed() + ( move:GetMaxSpeed() * speedBoostPerc )
		local newSpeed = speedAddition + move:GetVelocity():Length2D()

		-- Reverse it if the player is running backwards
		local isBackwards = move:GetVelocity():Dot( forward ) < 0

		-- Clamp it to make sure they can't bunnyhop to ludicrous speed
		-- Only apply cap if moving backwards (allows infinite forward bhop)
		if isBackwards and newSpeed > maxSpeed then
			speedAddition = speedAddition - ( newSpeed - maxSpeed )
		end

		-- Apply the speed boost
		move:SetVelocity( forward * speedAddition + move:GetVelocity() )
	end

	JUMPING = nil

end

player_manager.RegisterClass( "player_campaign", PLAYER, "player_default" )
