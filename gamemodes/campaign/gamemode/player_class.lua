DEFINE_BASECLASS( "player_default" )

local PLAYER = {}

PLAYER.DisplayName			= "Campaign Player"
PLAYER.SlowWalkSpeed		= 90
PLAYER.WalkSpeedNoSuit		= 150
PLAYER.WalkSpeed			= 190
PLAYER.RunSpeed				= 320 
PLAYER.DropWeaponOnDie		= false
PLAYER.JumpPower			= 160

function PLAYER:Spawn()
	BaseClass.Spawn( self )
	self.Player:EquipSuit()
	
	-- local ply = self.Player
	-- if ( ply:GetPData("campaign_has_suit", "0") == "1" ) then
	-- 	ply:EquipSuit()
	-- else
	-- 	ply:RemoveSuit()
	-- end

	--remove suit on intro maps
	local mapname = game.GetMap()
	if mapname == "d1_trainstation_01" or
		mapname == "d1_trainstation_02" or
		mapname == "d1_trainstation_03" or
		mapname == "d1_trainstation_04" or
		mapname == "d1_trainstation_05" then
		self.Player:RemoveSuit()
	end
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
local MAXSPEED

function PLAYER:StartMove( move )

	-- if no suit, speed is 150
	if not self.Player:IsSuitEquipped() then
		self.Player:SetWalkSpeed(self.WalkSpeedNoSuit)
		return
	else
		self.Player:SetWalkSpeed(self.WalkSpeed)
	end

	-- Only apply the jump boost in FinishMove if the player has jumped during this frame
	-- Using a global variable is safe here because nothing else happens between SetupMove and FinishMove
	if bit.band( move:GetButtons(), IN_JUMP ) ~= 0 and bit.band( move:GetOldButtons(), IN_JUMP ) == 0 and self.Player:OnGround() then
		JUMPING = true
	end

	MAXSPEED = move:GetMaxSpeed()

end

local USE_OLDENGINE_BUNNYHOP = true  

function PLAYER:FinishMove( move )

	-- If the player has jumped this frame
	if ( JUMPING ) then
        
		-- Get their orientation
		local forward = move:GetAngles()
		forward.p = 0
		forward = forward:Forward()

		-- Check if moving forward
		local forwardMove = move:GetForwardSpeed()
		if USE_OLDENGINE_BUNNYHOP and forwardMove > 0 then
			-- HL2 old engine speed boost logic (for forward movement)
			forward.z = 0
			forward:Normalize()

			local speedBoostScale
			if not move:KeyDown( IN_SPEED ) and not self.Player:Crouching() then
				-- Not sprinting and not ducked: 0.5x forward move
				speedBoostScale = 0.5
			else
				-- Sprinting or ducked: 0.1x forward move
				speedBoostScale = 0.1
			end

			-- Apply the forward speed boost
			local vecBoost = Vector( forward.x * forwardMove * speedBoostScale, forward.y * forwardMove * speedBoostScale, 0 )
			move:SetVelocity( move:GetVelocity() + vecBoost )
		else
			-- Original logic for backwards/no movement
			local moveMaxSpeed = MAXSPEED
			local speedBoostPerc = ( ( not move:KeyDown( IN_SPEED ) ) and ( not self.Player:Crouching() ) and 0.5 ) or 0.1

			local speedAddition = math.abs( forwardMove * speedBoostPerc )
			local maxSpeed = moveMaxSpeed + ( moveMaxSpeed * speedBoostPerc )
			local newSpeed = speedAddition + move:GetVelocity():Length2D()

			-- Reverse it if the player is running backwards
			local isBackwards = move:GetVelocity():Dot( forward ) < 0

			-- Clamp it to make sure they can't bunnyhop to ludicrous speed
			if isBackwards and newSpeed > maxSpeed then
				speedAddition = speedAddition - ( newSpeed - maxSpeed )
			end

			if ( forwardMove < 0 ) then
				speedAddition = -speedAddition
			end

			-- Apply the speed boost
			move:SetVelocity( forward * speedAddition + move:GetVelocity() )
		end
	end

	JUMPING = nil

end

player_manager.RegisterClass( "player_campaign", PLAYER, "player_default" )

--
-- HEV Suit pickup animation (clientside rendering, no weapon needed)
--

if SERVER then
    util.AddNetworkString( "PlaySuitIntroAnimation" )
    
    hook.Add( "PlayerCanPickupItem", "CampaignSuitPickup", function( ply, item )
        if item:GetClass() == "item_suit" then
            timer.Simple( 0.1, function()
                if IsValid( ply ) and game.GetMap() == "d1_trainstation_05" then
                    net.Start( "PlaySuitIntroAnimation" )
                    net.Send( ply )
                end
            end )
        end
    end )
end

if CLIENT then
    local SuitIntro = {
        Active = false,
        Model = nil,
        HandsModel = nil,
        StartTime = 0,
        Duration = 4.5, -- Animation duration in seconds
        LastEyeAngles = Angle( 0, 0, 0 ),
        SwayAngles = Angle( 0, 0, 0 )
    }
    
    local VIEWMODEL_FOV = 54
    local SWAY_SCALE = 1.5
    local SWAY_SPEED = 5
    
    local function StartSuitAnimation()
        -- Clean up any existing models
        if IsValid( SuitIntro.Model ) then
            SuitIntro.Model:Remove()
        end
        if IsValid( SuitIntro.HandsModel ) then
            SuitIntro.HandsModel:Remove()
        end
        
        local ply = LocalPlayer()
        
        -- Use the HL2 v_hands model specifically for the HEV suit animation
        local handsModel = "models/weapons/v_hands.mdl"
        
        -- Create the clientside hands model (the v_hands base)
        SuitIntro.Model = ClientsideModel( handsModel, RENDERGROUP_VIEWMODEL )
        if not IsValid( SuitIntro.Model ) then return end
        
        SuitIntro.Model:SetNoDraw( true )
        
        -- Get the player's C hands model and bonemerge it
        local handsInfo = player_manager.TranslatePlayerHands( player_manager.TranslateToPlayerModelName( ply:GetModel() ) )
        if handsInfo and handsInfo.model then
            SuitIntro.HandsModel = ClientsideModel( handsInfo.model, RENDERGROUP_VIEWMODEL )
            if IsValid( SuitIntro.HandsModel ) then
                SuitIntro.HandsModel:SetNoDraw( true )
                SuitIntro.HandsModel:SetParent( SuitIntro.Model )
                SuitIntro.HandsModel:AddEffects( EF_BONEMERGE )
                SuitIntro.HandsModel:AddEffects( EF_BONEMERGE_FASTCULL )
                
                -- Apply skin if specified
                if handsInfo.skin then
                    SuitIntro.HandsModel:SetSkin( handsInfo.skin )
                end
                if handsInfo.body then
                    SuitIntro.HandsModel:SetBodyGroups( handsInfo.body )
                end
            end
        end
        
        -- The v_hands.mdl draw sequence is the "looking at hands" animation
        local drawSeq = SuitIntro.Model:SelectWeightedSequence( ACT_VM_DRAW )
        
        -- Fallback to sequence 0 if not found
        if not drawSeq or drawSeq == -1 then
            drawSeq = 0
        end
        
        -- Use ResetSequence to properly start the animation
        SuitIntro.Model:ResetSequence( drawSeq )
        SuitIntro.Model:SetPlaybackRate( 1 )
        SuitIntro.Model:SetCycle( 0 )
        
        -- Get the animation duration
        local seqDuration = SuitIntro.Model:SequenceDuration( drawSeq )
        if seqDuration and seqDuration > 0 then
            SuitIntro.Duration = seqDuration
        end
        
        SuitIntro.Active = true
        SuitIntro.StartTime = CurTime()
        SuitIntro.LastEyeAngles = ply:EyeAngles()
        SuitIntro.SwayAngles = Angle( 0, 0, 0 )
        SuitIntro.LastFrameTime = CurTime()
    end
    
    local function StopSuitAnimation()
        if IsValid( SuitIntro.HandsModel ) then
            SuitIntro.HandsModel:Remove()
            SuitIntro.HandsModel = nil
        end
        if IsValid( SuitIntro.Model ) then
            SuitIntro.Model:Remove()
            SuitIntro.Model = nil
        end
        SuitIntro.Active = false
    end
    
    -- Receive the signal from server to play animation
    net.Receive( "PlaySuitIntroAnimation", function()
        StartSuitAnimation()
    end )
    
    -- Calculate view sway based on eye angle changes
    local function CalcViewSway( ply )
        local eyeAng = ply:EyeAngles()
        local dt = FrameTime()
        
        -- Calculate angular velocity
        local angDiff = Angle(
            math.AngleDifference( eyeAng.p, SuitIntro.LastEyeAngles.p ),
            math.AngleDifference( eyeAng.y, SuitIntro.LastEyeAngles.y ),
            0
        )
        
        -- Smooth sway
        SuitIntro.SwayAngles.p = Lerp( dt * SWAY_SPEED, SuitIntro.SwayAngles.p, -angDiff.p * SWAY_SCALE )
        SuitIntro.SwayAngles.y = Lerp( dt * SWAY_SPEED, SuitIntro.SwayAngles.y, -angDiff.y * SWAY_SCALE )
        SuitIntro.SwayAngles.r = SuitIntro.SwayAngles.y * 0.5
        
        -- Decay sway back to zero
        SuitIntro.SwayAngles.p = Lerp( dt * 3, SuitIntro.SwayAngles.p, 0 )
        SuitIntro.SwayAngles.y = Lerp( dt * 3, SuitIntro.SwayAngles.y, 0 )
        SuitIntro.SwayAngles.r = Lerp( dt * 3, SuitIntro.SwayAngles.r, 0 )
        
        SuitIntro.LastEyeAngles = eyeAng
        
        return SuitIntro.SwayAngles
    end
    
    -- Render the hands model
    hook.Add( "PreDrawViewModel", "DrawSuitIntroHands", function( vm, ply, weapon )
        if not SuitIntro.Active then return end
        if not IsValid( SuitIntro.Model ) then return end
        
        -- Check if animation is done
        if CurTime() - SuitIntro.StartTime > SuitIntro.Duration then
            StopSuitAnimation()
            return
        end
        
        -- Update animation
        SuitIntro.Model:FrameAdvance()
        
        -- Get view position and angles
        local viewPos = EyePos()
        local viewAng = EyeAngles()
        
        -- Apply sway
        local sway = CalcViewSway( ply )
        viewAng:RotateAroundAxis( viewAng:Right(), sway.p )
        viewAng:RotateAroundAxis( viewAng:Up(), sway.y )
        viewAng:RotateAroundAxis( viewAng:Forward(), sway.r )
        
        -- Position the hands in front of the view (adjusted position)
        local pos = viewPos + viewAng:Forward() * 5
        
        SuitIntro.Model:SetPos( pos )
        SuitIntro.Model:SetAngles( viewAng )
        SuitIntro.Model:SetupBones()
        
        -- Render with viewmodel FOV
        cam.IgnoreZ( true )
        -- Hide the v_hands model but still need to set it up for bonemerge
        SuitIntro.Model:SetMaterial( "engine/occlusionproxy" )
        SuitIntro.Model:DrawModel()
        SuitIntro.Model:SetMaterial( "" )
        if IsValid( SuitIntro.HandsModel ) then
            SuitIntro.HandsModel:SetupBones()
            SuitIntro.HandsModel:DrawModel()
        end
        cam.IgnoreZ( false )
    end )
    
    -- Fallback: Also render in PostRender if no weapon is equipped
    hook.Add( "PostDrawOpaqueRenderables", "DrawSuitIntroHandsFallback", function( depth, sky )
        if sky then return end
        if not SuitIntro.Active then return end
        if not IsValid( SuitIntro.Model ) then return end
        
        local ply = LocalPlayer()
        if not IsValid( ply ) then return end
        
        -- Skip if PreDrawViewModel already handled it (player has a weapon)
        local wep = ply:GetActiveWeapon()
        if IsValid( wep ) and wep:GetClass() ~= "weapon_fists" then return end
        
        -- Check if animation is done
        if CurTime() - SuitIntro.StartTime > SuitIntro.Duration then
            StopSuitAnimation()
            return
        end
        
        -- Update animation
        SuitIntro.Model:FrameAdvance()
        
        -- Get view position and angles
        local viewPos = EyePos()
        local viewAng = EyeAngles()
        
        -- Apply sway
        local sway = CalcViewSway( ply )
        local renderAng = Angle( viewAng.p, viewAng.y, viewAng.r )
        renderAng:RotateAroundAxis( renderAng:Right(), sway.p )
        renderAng:RotateAroundAxis( renderAng:Up(), sway.y )
        renderAng:RotateAroundAxis( renderAng:Forward(), sway.r )
        
        -- Position the hands in front of the view (adjusted position)
        local pos = viewPos + renderAng:Forward() * 5
        
        SuitIntro.Model:SetPos( pos )
        SuitIntro.Model:SetAngles( renderAng )
        SuitIntro.Model:SetupBones()
        
        -- Draw in a separate 3D context with viewmodel FOV
        cam.Start3D( viewPos, viewAng, VIEWMODEL_FOV, nil, nil, nil, nil, 1, 100 )
            cam.IgnoreZ( true )
            -- Hide the v_hands model but still need to set it up for bonemerge
            SuitIntro.Model:SetMaterial( "engine/occlusionproxy" )
            SuitIntro.Model:DrawModel()
            SuitIntro.Model:SetMaterial( "" )
            if IsValid( SuitIntro.HandsModel ) then
                SuitIntro.HandsModel:SetupBones()
                SuitIntro.HandsModel:DrawModel()
            end
            cam.IgnoreZ( false )
        cam.End3D()
    end )
    
    -- Clean up on disconnect/map change
    hook.Add( "ShutDown", "CleanupSuitIntro", StopSuitAnimation )
    hook.Add( "OnReloaded", "CleanupSuitIntro", StopSuitAnimation )
end
