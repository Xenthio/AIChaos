local hook_name = "AIChaos_MirrorMode"

if SERVER then
    util.AddNetworkString("AIChaos_Mirror_Start")
    util.AddNetworkString("AIChaos_Mirror_Stop")

    concommand.Add("aichaos_mirror_start", function(ply, cmd, args)
        if not IsValid(ply) then return end
        
        -- Send to client to start visuals
        net.Start("AIChaos_Mirror_Start")
        net.Send(ply)
        
        -- Server side physics chaos
        local duration = 4.0
        local startTime = CurTime()
        local nextSlap = CurTime() + 1
        
        local uniqueName = hook_name .. "_ServerSpin_" .. ply:SteamID64()
        
        hook.Add("Think", uniqueName, function()
            if not IsValid(ply) then hook.Remove("Think", uniqueName) return end
            
            local t = CurTime() - startTime
            if t > duration then
                hook.Remove("Think", uniqueName)
                return
            end
            
            if CurTime() > nextSlap then
                -- Random velocity
                local vel = Vector(math.random(-500, 500), math.random(-500, 500), math.random(200, 300))
                ply:SetVelocity(vel)
                
                nextSlap = CurTime() + math.random(1, 1)
            end
        end)
    end)
    
    concommand.Add("aichaos_mirror_stop", function(ply, cmd, args)
        if not IsValid(ply) then return end
        net.Start("AIChaos_Mirror_Stop")
        net.Send(ply)
        
        local uniqueName = hook_name .. "_ServerSpin_" .. ply:SteamID64()
        hook.Remove("Think", uniqueName)
    end)
end

if CLIENT then
    print("AIChaos Mirror Mode: Chaos Spin Edition")
    
    net.Receive("AIChaos_Mirror_Start", function()
        AIChaos_StartMirrorChaos()
    end)
    
    net.Receive("AIChaos_Mirror_Stop", function()
        AIChaos_StopMirrorChaos()
    end)

-- State Variables
local MirrorActive = false
local SpinActive = false
local SpinStartTime = 0
local SpinDuration = 4.0 -- Total duration of the spin effect
local SpinPeakTime = 2.0 -- When the spin is fastest (and when we swap)
local SpinMaxSpeed = 40  -- Max degrees per tick
local SpinDirection = 1  -- 1 or -1

-- Global Functions to Control the Effect
function AIChaos_StartMirrorChaos()
    MirrorActive = false -- Start normal
    SpinActive = true
    SpinStartTime = CurTime()
    SpinDirection = math.random(0, 1) == 1 and 1 or -1
    
    -- Play a sound?
    surface.PlaySound("ambient/machines/spinup.wav")
end

function AIChaos_StopMirrorChaos()
    MirrorActive = false
    SpinActive = false
    
    -- Clean up any entities that might still have the callback
    local ply = LocalPlayer()
    if IsValid(ply) then
        local vm = ply:GetViewModel()
        if IsValid(vm) and vm.AIChaos_FlipSetup then
            vm:RemoveCallback("BuildBonePositions", vm.AIChaos_FlipCallbackID)
            vm.AIChaos_FlipSetup = false
            vm.AIChaos_FlipCallbackID = nil
        end
        
        local hands = ply:GetHands()
        if IsValid(hands) and hands.AIChaos_FlipSetup then
            hands:RemoveCallback("BuildBonePositions", hands.AIChaos_FlipCallbackID)
            hands.AIChaos_FlipSetup = false
            hands.AIChaos_FlipCallbackID = nil
        end
    end
end

-- Clean up old hooks
hook.Remove("RenderScene", hook_name)
hook.Remove("RenderScreenspaceEffects", hook_name)
hook.Remove("PreDrawViewModel", hook_name)
hook.Remove("PostDrawViewModel", hook_name)
hook.Remove("CalcViewModelView", hook_name)
hook.Remove("EntityRemoved", hook_name)
hook.Remove("Think", hook_name)
hook.Remove("FireAnimationEvent", hook_name)
hook.Remove("EntityFireBullets", hook_name)
hook.Remove("PreDrawPlayerHands", hook_name)
hook.Remove("PostDrawPlayerHands", hook_name)
hook.Remove("DrawPhysgunBeam", hook_name)
hook.Remove("CreateMove", hook_name)
hook.Remove("InputMouseApply", hook_name)
hook.Remove("PostDrawEffects", hook_name)


-- ============================================================================
-- 1. Spin Logic (The Chaos)
-- ============================================================================

hook.Add("CreateMove", hook_name, function(cmd)
    if SpinActive then
        local t = CurTime() - SpinStartTime
        
        -- Calculate Spin Speed based on time (Trapezoid or Triangle profile)
        local speed = 0
        
        if t < SpinPeakTime then
            -- Spin Up
            local progress = t / SpinPeakTime
            -- Ease In Quad
            speed = SpinMaxSpeed * (progress * progress)
        elseif t < SpinDuration then
            -- Spin Down
            local progress = (t - SpinPeakTime) / (SpinDuration - SpinPeakTime)
            -- Ease Out Quad
            speed = SpinMaxSpeed * (1 - (progress * progress))
            
            -- Activate Mirror Mode at the peak
            if not MirrorActive then
                MirrorActive = true
                SpinDirection = -SpinDirection -- Invert spin so it looks consistent through the mirror
                surface.PlaySound("buttons/combine_button7.wav") -- Glitch sound
            end
        else
            -- End Spin
            SpinActive = false
            speed = 0
        end
        
        -- Apply Spin
        local ang = cmd:GetViewAngles()
        ang.y = ang.y + (speed * SpinDirection)
        cmd:SetViewAngles(ang)
    end
    
    -- ========================================================================
    -- 2. Mirror Controls (Only when MirrorActive)
    -- ========================================================================
    if MirrorActive then
        -- Flip analog movement
        cmd:SetSideMove(-cmd:GetSideMove())
        
        -- Flip buttons (important for vehicles that check buttons directly)
        local buttons = cmd:GetButtons()
        local left = bit.band(buttons, IN_MOVELEFT) == IN_MOVELEFT
        local right = bit.band(buttons, IN_MOVERIGHT) == IN_MOVERIGHT
        
        if left and not right then
            buttons = bit.band(buttons, bit.bnot(IN_MOVELEFT))
            buttons = bit.bor(buttons, IN_MOVERIGHT)
        elseif right and not left then
            buttons = bit.band(buttons, bit.bnot(IN_MOVERIGHT))
            buttons = bit.bor(buttons, IN_MOVELEFT)
        end
        
        cmd:SetButtons(buttons)
    end
end)

hook.Add("InputMouseApply", hook_name, function(cmd, x, y, ang)
    if not MirrorActive then return end
    
    local view_ang = cmd:GetViewAngles()
    local pitch = GetConVar("m_pitch"):GetFloat()
    local yaw = GetConVar("m_yaw"):GetFloat()
    
    -- Invert Mouse X
    view_ang.y = view_ang.y + (x * yaw) -- Normally -(x * yaw), so adding it flips it? 
    -- Wait, standard is: ang.y -= x * m_yaw. So += flips it.
    
    view_ang.p = view_ang.p + (y * pitch)
    
    if view_ang.p > 89 then view_ang.p = 89 end
    if view_ang.p < -89 then view_ang.p = -89 end
    
    cmd:SetViewAngles(view_ang)
    return true
end)


-- ============================================================================
-- 3. Visual Flipping (Screen & Models)
-- ============================================================================

-- Flip Screen
hook.Add("PostDrawEffects", hook_name, function()
    if not MirrorActive then return end
    
    local w, h = ScrW(), ScrH()
    render.CopyRenderTargetToTexture(render.GetScreenEffectTexture())
    render.DrawTextureToScreenRect(render.GetScreenEffectTexture(), w, 0, -w, h)
end)

-- Bone Manipulation Helpers
local function SetupFlip(ent)
    if not IsValid(ent) then return end
    if ent.AIChaos_FlipSetup then return end
    
    ent.AIChaos_FlipCallbackID = ent:AddCallback( "BuildBonePositions", function(self, count)
        -- Precompute the transformation matrix
        -- M_final = Cam * Scale * View * M_bone
        
        local eyePos = EyePos()
        local eyeAng = EyeAngles()
        
        local camMat = Matrix()
        camMat:SetTranslation(eyePos)
        camMat:SetAngles(eyeAng)
        
        local viewMat = camMat:GetInverse()
        
        local scaleMat = Matrix()
        scaleMat:Scale(Vector(1, -1, 1))
        
        -- Combine: T = Cam * Scale * View
        local transform = camMat * scaleMat * viewMat
        
        for i = 0, count - 1 do
            local boneMat = self:GetBoneMatrix(i)
            if boneMat then
                self:SetBoneMatrix(i, transform * boneMat)
            end
        end
    end)
    ent.AIChaos_FlipSetup = true
end

local function RemoveFlip(ent)
    if not IsValid(ent) then return end
    if not ent.AIChaos_FlipSetup then return end
    
    if ent.AIChaos_FlipCallbackID then
        ent:RemoveCallback("BuildBonePositions", ent.AIChaos_FlipCallbackID)
    end
    ent.AIChaos_FlipSetup = false
    ent.AIChaos_FlipCallbackID = nil
end

-- Flip ViewModel & Hands
hook.Add("PreDrawViewModel", hook_name, function(vm, ply, weapon)
    if not MirrorActive then 
        -- Ensure we clean up if mirror mode was disabled
        if vm.AIChaos_FlipSetup then RemoveFlip(vm) end
        local hands = ply:GetHands()
        if IsValid(hands) and hands.AIChaos_FlipSetup then RemoveFlip(hands) end
        return 
    end

    SetupFlip(vm)
    
    -- Force bone cache invalidation to prevent 1-frame lag
    vm:InvalidateBoneCache()
    vm:SetupBones()
    
    local hands = ply:GetHands()
    if IsValid(hands) then
        -- Only flip hands if they are NOT bonemerged to the VM (to avoid double-flip)
        if hands:GetParent() ~= vm then
            SetupFlip(hands)
        else
            -- If they are parented, we assume they inherit the flip.
            RemoveFlip(hands)
            
            -- But we still need to invalidate cache to ensure they copy the NEW positions.
            hands:InvalidateBoneCache()
            hands:SetupBones()
        end
    end
    
    -- We still need to invert culling because the geometry is flipped
    render.CullMode(MATERIAL_CULLMODE_CW)
end)

hook.Add("PostDrawViewModel", hook_name, function(vm, ply, weapon)
    if not MirrorActive then return end
    render.CullMode(MATERIAL_CULLMODE_CCW)
end)

hook.Add("PreDrawPlayerHands", hook_name, function(hands, vm, ply, weapon)
    if not MirrorActive then return end
    render.CullMode(MATERIAL_CULLMODE_CW)
end)

hook.Add("PostDrawPlayerHands", hook_name, function(hands, vm, ply, weapon)
    if not MirrorActive then return end
    render.CullMode(MATERIAL_CULLMODE_CCW)
end)

-- Fix Physgun Beam
hook.Add("DrawPhysgunBeam", hook_name, function(ply, wep, on, target, bone, pos)
    if not MirrorActive then return end
    if ply == LocalPlayer() then
        local vm = ply:GetViewModel()
        if IsValid(vm) then
            vm:InvalidateBoneCache()
            vm:SetupBones()
        end
    end
end)

-- Fix Attachments (Shells/Muzzleflashes)
hook.Add("FireAnimationEvent", hook_name, function(pos, ang, event, options, wep)
    if not MirrorActive then return end
    
    -- 20 = Shell Eject, 21 = Shell Eject, 6001 = Muzzle Flash
    if event == 20 or event == 21 or event == 6001 then
        if not IsValid(wep) then return end
        
        local ply = wep:GetOwner()
        if IsValid(ply) and ply:IsPlayer() and ply == LocalPlayer() then
            local vm = ply:GetViewModel()
            if IsValid(vm) then
                SetupFlip(vm)
                vm:InvalidateBoneCache()
                vm:SetupBones()

                local attId = 2
                if event == 6001 then attId = 1 end
                
                local att = vm:GetAttachment(attId)
                if att then
                    if event == 20 or event == 21 then
                        local data = EffectData()
                        data:SetOrigin(att.Pos)
                        data:SetAngles(att.Ang)
                        data:SetEntity(wep)
                        data:SetAttachment(attId)
                        util.Effect("ShellEject", data)
                    elseif event == 6001 then
                         local data = EffectData()
                        data:SetEntity(wep)
                        data:SetOrigin(att.Pos)
                        data:SetAngles(att.Ang)
                        data:SetScale(1)
                        data:SetAttachment(1)
                        util.Effect("MuzzleFlash", data)
                    end
                    return true -- Suppress default
                end
            end
        end
    end
end)

-- Auto-start for testing (optional, remove later)
-- concommand.Add("aichaos_mirror_start", AIChaos_StartMirrorChaos)
-- concommand.Add("aichaos_mirror_stop", AIChaos_StopMirrorChaos)

end -- End of if CLIENT
