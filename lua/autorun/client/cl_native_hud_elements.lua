if SERVER then return end

-- ============================================================================
--  Native HUD Elements - Port of Source SDK 2013 HUD elements
--  
--  These are the core HUD elements from Half-Life 2, ported to Lua using
--  the CHudElement base class system.
-- ============================================================================

-- Wait for dependencies to load
timer.Simple(0.2, function()
    if not CHudElement or not HudTheme or not ChaosHUD then
        print("[NativeHUD] Error: Required dependencies not loaded!")
        return
    end

    -- ============================================================================
    --  HudHealth - Health display
    -- ============================================================================

    local HudHealth = setmetatable({}, { __index = CHudElement })
    HudHealth.__index = HudHealth

    function HudHealth:New()
        local obj = CHudElement.New(self, "CHudHealth")
        setmetatable(obj, self)
        
        obj.m_iHealth = 0
        obj.m_flNextHealthPulse = 0
        obj.m_flHealthChangeTime = 0
        obj.m_iLastHealth = 0
        
        obj:SetHiddenBits(HIDEHUD_HEALTH + HIDEHUD_PLAYERDEAD + HIDEHUD_NEEDSUIT)
        
        return obj
    end

    function HudHealth:Init()
        self.m_iHealth = 100
    end

    function HudHealth:Reset()
        self.m_iHealth = 100
        self.m_flHealthChangeTime = 0
    end

    function HudHealth:ShouldDraw()
        if not CHudElement.ShouldDraw(self) then
            return false
        end
        
        local ply = LocalPlayer()
        if not IsValid(ply) then return false end
        
        -- Hide in vehicles
        if ply:InVehicle() then
            local veh = ply:GetVehicle()
            if IsValid(veh) and veh:GetClass() == "prop_vehicle_jeep" then
                return false
            end
        end
        
        return true
    end

    function HudHealth:Think()
        local ply = LocalPlayer()
        if not IsValid(ply) then return end
        
        local health = ply:Health()
        if health ~= self.m_iLastHealth then
            self.m_flHealthChangeTime = CurTime()
            self.m_iLastHealth = health
        end
        
        self.m_iHealth = health
    end

    function HudHealth:Paint()
        if not self:ShouldDraw() then return end
        
        self:Think()
        
        -- Use ChaosHUD drawing primitives for consistency
        local scale = ChaosHUD.GetScale()
        local x = 16 * scale
        local y = ScrH() - (48 * scale)
        
        ChaosHUD.DrawNumericDisplay(
            x, y,
            "HEALTH",
            self.m_iHealth,
            self.m_flHealthChangeTime,
            { warn_low = true, low_threshold = 20 }
        )
    end

    -- Register the element
    local g_HudHealth = HudHealth:New()
    g_HudHealth:Init()

    -- ============================================================================
    --  HudSuit - Armor/Suit power display
    -- ============================================================================

    local HudSuit = setmetatable({}, { __index = CHudElement })
    HudSuit.__index = HudSuit

    function HudSuit:New()
        local obj = CHudElement.New(self, "CHudSuit")
        setmetatable(obj, self)
        
        obj.m_iSuit = 0
        obj.m_flSuitChangeTime = 0
        obj.m_iLastSuit = 0
        
        obj:SetHiddenBits(HIDEHUD_HEALTH + HIDEHUD_PLAYERDEAD + HIDEHUD_NEEDSUIT)
        
        return obj
    end

    function HudSuit:Init()
        self.m_iSuit = 0
    end

    function HudSuit:Reset()
        self.m_iSuit = 0
        self.m_flSuitChangeTime = 0
    end

    function HudSuit:ShouldDraw()
        if not CHudElement.ShouldDraw(self) then
            return false
        end
        
        local ply = LocalPlayer()
        if not IsValid(ply) then return false end
        
        -- Only show if player has armor
        if ply:Armor() <= 0 then
            return false
        end
        
        -- Hide in vehicles
        if ply:InVehicle() then
            local veh = ply:GetVehicle()
            if IsValid(veh) and veh:GetClass() == "prop_vehicle_jeep" then
                return false
            end
        end
        
        return true
    end

    function HudSuit:Think()
        local ply = LocalPlayer()
        if not IsValid(ply) then return end
        
        local armor = ply:Armor()
        if armor ~= self.m_iLastSuit then
            self.m_flSuitChangeTime = CurTime()
            self.m_iLastSuit = armor
        end
        
        self.m_iSuit = armor
    end

    function HudSuit:Paint()
        if not self:ShouldDraw() then return end
        
        self:Think()
        
        local scale = ChaosHUD.GetScale()
        local x = 140 * scale
        local y = ScrH() - (48 * scale)
        
        ChaosHUD.DrawNumericDisplay(
            x, y,
            "SUIT",
            self.m_iSuit,
            self.m_flSuitChangeTime,
            { warn_low = false }
        )
    end

    -- Register the element
    local g_HudSuit = HudSuit:New()
    g_HudSuit:Init()

    -- ============================================================================
    --  HudAmmo - Ammunition display
    -- ============================================================================

    local HudAmmo = setmetatable({}, { __index = CHudElement })
    HudAmmo.__index = HudAmmo

    function HudAmmo:New()
        local obj = CHudElement.New(self, "CHudAmmo")
        setmetatable(obj, self)
        
        obj.m_iAmmo = 0
        obj.m_iAmmo2 = 0
        obj.m_flAmmoChangeTime = 0
        obj.m_flWeaponChangeTime = 0
        obj.m_iLastAmmo = 0
        obj.m_hLastWeapon = nil
        
        obj:SetHiddenBits(HIDEHUD_WEAPONSELECTION + HIDEHUD_PLAYERDEAD + HIDEHUD_NEEDSUIT)
        
        return obj
    end

    function HudAmmo:Init()
        self.m_iAmmo = 0
        self.m_iAmmo2 = 0
    end

    function HudAmmo:Reset()
        self.m_iAmmo = 0
        self.m_iAmmo2 = 0
        self.m_flAmmoChangeTime = 0
        self.m_flWeaponChangeTime = 0
    end

    function HudAmmo:ShouldDraw()
        if not CHudElement.ShouldDraw(self) then
            return false
        end
        
        local ply = LocalPlayer()
        if not IsValid(ply) then return false end
        
        local wpn = ply:GetActiveWeapon()
        if not IsValid(wpn) then return false end
        
        -- Don't show for weapons without ammo
        if wpn:GetPrimaryAmmoType() == -1 then
            return false
        end
        
        -- Hide in vehicles
        if ply:InVehicle() then
            local veh = ply:GetVehicle()
            if IsValid(veh) and veh:GetClass() == "prop_vehicle_jeep" then
                return false
            end
        end
        
        return true
    end

    function HudAmmo:Think()
        local ply = LocalPlayer()
        if not IsValid(ply) then return end
        
        local wpn = ply:GetActiveWeapon()
        if not IsValid(wpn) then return end
        
        -- Check for weapon change
        if wpn ~= self.m_hLastWeapon then
            self.m_flWeaponChangeTime = CurTime()
            self.m_hLastWeapon = wpn
        end
        
        -- Get ammo counts
        local ammo = 0
        local ammo2 = nil
        
        if wpn:GetMaxClip1() ~= -1 then
            -- Uses clips
            ammo = wpn:Clip1()
            
            -- Secondary ammo (reserve)
            local ammoType = wpn:GetPrimaryAmmoType()
            if ammoType ~= -1 then
                ammo2 = ply:GetAmmoCount(ammoType)
            end
        else
            -- No clips, show reserve ammo
            local ammoType = wpn:GetPrimaryAmmoType()
            if ammoType ~= -1 then
                ammo = ply:GetAmmoCount(ammoType)
            end
        end
        
        -- Track changes
        if ammo ~= self.m_iLastAmmo then
            self.m_flAmmoChangeTime = CurTime()
            self.m_iLastAmmo = ammo
        end
        
        self.m_iAmmo = ammo
        self.m_iAmmo2 = ammo2
    end

    function HudAmmo:Paint()
        if not self:ShouldDraw() then return end
        
        self:Think()
        
        local scale = ChaosHUD.GetScale()
        local theme = HudTheme.GetCurrent()
        
        -- Calculate position (right-aligned)
        -- In Source SDK, "r150" means left edge is 150 pixels from right edge
        local layout = theme.Layout.HudAmmo
        local x = HudResources.ConvertPosition(layout.xpos, ScrW())
        -- Position from bottom like SDK (ypos 432 from bottom at 480 height = 48 from bottom)
        local y = ScrH() - (48 * scale)
        
        ChaosHUD.DrawAmmoDisplay(
            x, y,
            "AMMO",
            self.m_iAmmo,
            self.m_iAmmo2,
            self.m_flAmmoChangeTime,
            self.m_flWeaponChangeTime,
            {}
        )
    end

    -- Register the element
    local g_HudAmmo = HudAmmo:New()
    g_HudAmmo:Init()

    -- ============================================================================
    --  HudAmmoSecondary - Secondary ammunition display (for alt-fire weapons)
    -- ============================================================================

    local HudAmmoSecondary = setmetatable({}, { __index = CHudElement })
    HudAmmoSecondary.__index = HudAmmoSecondary

    function HudAmmoSecondary:New()
        local obj = CHudElement.New(self, "CHudAmmoSecondary")
        setmetatable(obj, self)
        
        obj.m_iAmmo = 0
        obj.m_flAmmoChangeTime = 0
        obj.m_iLastAmmo = 0
        obj.m_hLastWeapon = nil
        
        obj:SetHiddenBits(HIDEHUD_WEAPONSELECTION + HIDEHUD_PLAYERDEAD + HIDEHUD_NEEDSUIT)
        
        return obj
    end

    function HudAmmoSecondary:Init()
        self.m_iAmmo = 0
    end

    function HudAmmoSecondary:Reset()
        self.m_iAmmo = 0
        self.m_flAmmoChangeTime = 0
    end

    function HudAmmoSecondary:ShouldDraw()
        if not CHudElement.ShouldDraw(self) then
            return false
        end
        
        local ply = LocalPlayer()
        if not IsValid(ply) then return false end
        
        local wpn = ply:GetActiveWeapon()
        if not IsValid(wpn) then return false end
        
        -- Only show for weapons with secondary ammo type
        if wpn:GetSecondaryAmmoType() == -1 then
            return false
        end
        
        -- Hide in vehicles
        if ply:InVehicle() then
            local veh = ply:GetVehicle()
            if IsValid(veh) and veh:GetClass() == "prop_vehicle_jeep" then
                return false
            end
        end
        
        return true
    end

    function HudAmmoSecondary:Think()
        local ply = LocalPlayer()
        if not IsValid(ply) then return end
        
        local wpn = ply:GetActiveWeapon()
        if not IsValid(wpn) then return end
        
        -- Get secondary ammo count
        local ammo = 0
        local ammoType = wpn:GetSecondaryAmmoType()
        if ammoType ~= -1 then
            ammo = ply:GetAmmoCount(ammoType)
        end
        
        -- Track changes
        if ammo ~= self.m_iLastAmmo then
            self.m_flAmmoChangeTime = CurTime()
            self.m_iLastAmmo = ammo
        end
        
        self.m_iAmmo = ammo
    end

    function HudAmmoSecondary:Paint()
        if not self:ShouldDraw() then return end
        
        self:Think()
        
        local scale = ChaosHUD.GetScale()
        local theme = HudTheme.GetCurrent()
        
        -- Calculate position (right-aligned, to the right of primary ammo)
        local layout = theme.Layout.HudAmmoSecondary
        local x = HudResources.ConvertPosition(layout.xpos, ScrW())
        local y = ScrH() - (48 * scale)
        
        -- Draw as a smaller numeric display
        ChaosHUD.DrawNumericDisplay(
            x, y,
            "",  -- No label for secondary ammo
            self.m_iAmmo,
            self.m_flAmmoChangeTime,
            { warn_low = false }
        )
    end

    -- Register the element
    local g_HudAmmoSecondary = HudAmmoSecondary:New()
    g_HudAmmoSecondary:Init()

    -- ============================================================================
    --  HudSuitPower - Aux Power display (Sprint, Flashlight, Oxygen)
    -- ============================================================================

    local HudSuitPower = setmetatable({}, { __index = CHudElement })
    HudSuitPower.__index = HudSuitPower

    function HudSuitPower:New()
        local obj = CHudElement.New(self, "CHudSuitPower")
        setmetatable(obj, self)
        
        obj.m_flSuitPower = -1  -- Initialize to -1 like SDK
        obj.m_iActiveSuitDevices = 0
        obj.m_AnimatedHeight = 26  -- Start at minimum height
        obj.m_TargetHeight = 26
        obj.m_AnimatedY = 400  -- Start at base position
        obj.m_TargetY = 400
        obj.m_AnimSpeed = 0
        obj:SetHiddenBits(HIDEHUD_HEALTH + HIDEHUD_PLAYERDEAD + HIDEHUD_NEEDSUIT)
        
        return obj
    end

    function HudSuitPower:Init()
        self.m_flSuitPower = -1
        self.m_iActiveSuitDevices = 0
        self.m_AnimatedHeight = 26
        self.m_TargetHeight = 26
        self.m_AnimatedY = 400
        self.m_TargetY = 400
    end

    function HudSuitPower:ShouldDraw()
        if not CHudElement.ShouldDraw(self) then
            return false
        end
        
        local ply = LocalPlayer()
        if not IsValid(ply) then return false end
        
        -- Only show when aux power is being used or is low
        local suitPower = ply:GetSuitPower()
        local activeItems = self:GetActiveItems()
        
        if suitPower >= 100 and #activeItems == 0 then
            return false
        end
        
        return true
    end

    function HudSuitPower:GetActiveItems()
        local items = {}
        local ply = LocalPlayer()
        if not IsValid(ply) then return items end
        
        if ply:WaterLevel() == 3 then
            table.insert(items, "OXYGEN")
        end
        
        if ply:FlashlightIsOn() then
            table.insert(items, "FLASHLIGHT")
        end
        
        if ply:IsSprinting() and ply:GetVelocity():Length2D() > 1 then
            table.insert(items, "SPRINT")
        end
        
        return items
    end
    
    function HudSuitPower:Think()
        local ply = LocalPlayer()
        if not IsValid(ply) then return end
        
        local suitPower = ply:GetSuitPower()
        local activeItems = self:GetActiveItems()
        local numActiveDevices = #activeItems
        
        -- Check if number of active devices changed
        if numActiveDevices ~= self.m_iActiveSuitDevices then
            self.m_iActiveSuitDevices = numActiveDevices
            
            -- Determine target size and position based on active devices
            -- From HudAnimations.txt:
            -- NoItems: Size 102x26, Position y=400
            -- OneItem: Size 102x36, Position y=390
            -- TwoItems: Size 102x46, Position y=380
            -- ThreeItems: Size 102x56, Position y=370
            if numActiveDevices == 0 then
                self.m_TargetHeight = 26
                self.m_TargetY = 400
            elseif numActiveDevices == 1 then
                self.m_TargetHeight = 36
                self.m_TargetY = 390
            elseif numActiveDevices == 2 then
                self.m_TargetHeight = 46
                self.m_TargetY = 380
            else  -- 3 or more
                self.m_TargetHeight = 56
                self.m_TargetY = 370
            end
            
            -- Calculate animation speed (0.4 seconds like SDK)
            local heightDiff = math.abs(self.m_TargetHeight - self.m_AnimatedHeight)
            local yDiff = math.abs(self.m_TargetY - self.m_AnimatedY)
            self.m_AnimSpeed = math.max(heightDiff, yDiff) / 0.4
        end
        
        -- Animate height and position
        if self.m_AnimSpeed > 0 then
            local delta = self.m_AnimSpeed * FrameTime()
            
            -- Animate height
            if math.abs(self.m_TargetHeight - self.m_AnimatedHeight) < delta then
                self.m_AnimatedHeight = self.m_TargetHeight
            else
                if self.m_TargetHeight > self.m_AnimatedHeight then
                    self.m_AnimatedHeight = self.m_AnimatedHeight + delta
                else
                    self.m_AnimatedHeight = self.m_AnimatedHeight - delta
                end
            end
            
            -- Animate Y position
            if math.abs(self.m_TargetY - self.m_AnimatedY) < delta then
                self.m_AnimatedY = self.m_TargetY
            else
                if self.m_TargetY > self.m_AnimatedY then
                    self.m_AnimatedY = self.m_AnimatedY + delta
                else
                    self.m_AnimatedY = self.m_AnimatedY - delta
                end
            end
            
            -- Stop animating if we reached target
            if self.m_AnimatedHeight == self.m_TargetHeight and self.m_AnimatedY == self.m_TargetY then
                self.m_AnimSpeed = 0
            end
        end
        
        self.m_flSuitPower = suitPower
    end

    function HudSuitPower:Paint()
        if not self:ShouldDraw() then return end
        
        local ply = LocalPlayer()
        if not IsValid(ply) then return end
        
        self:Think()
        
        local activeItems = self:GetActiveItems()
        
        local scale = ChaosHUD.GetScale()
        local x = 16 * scale
        -- Use animated Y position
        local y = ScrH() - (48 * scale) - (self.m_AnimatedY * scale)
        local height = self.m_AnimatedHeight * scale
        
        -- Draw with proper label "AUX POWER"
        ChaosHUD.DrawAuxBar(x, y, "AUX POWER", self.m_flSuitPower, activeItems, height)
    end

    -- Register the element
    local g_HudSuitPower = HudSuitPower:New()
    g_HudSuitPower:Init()

    -- ============================================================================
    --  HudCrosshair - Crosshair display
    -- ============================================================================

    local HudCrosshair = setmetatable({}, { __index = CHudElement })
    HudCrosshair.__index = HudCrosshair

    function HudCrosshair:New()
        local obj = CHudElement.New(self, "CHudCrosshair")
        setmetatable(obj, self)
        
        obj:SetHiddenBits(HIDEHUD_CROSSHAIR + HIDEHUD_PLAYERDEAD)
        
        return obj
    end

    function HudCrosshair:ShouldDraw()
        if not CHudElement.ShouldDraw(self) then
            return false
        end
        
        -- Use GMod's native crosshair system
        local ply = LocalPlayer()
        if not IsValid(ply) then return false end
        
        local wpn = ply:GetActiveWeapon()
        if not IsValid(wpn) then return false end
        
        -- Let weapon override crosshair
        if wpn.DrawCrosshair == false then
            return false
        end
        
        return true
    end

    function HudCrosshair:Paint()
        if not self:ShouldDraw() then return end
        
        -- Simple HL2-style crosshair
        local x = ScrW() / 2
        local y = ScrH() / 2
        local size = 8
        local gap = 6
        local thickness = 2
        
        local theme = HudTheme.GetCurrent()
        local color = theme.Colors.BrightFg
        
        -- Draw crosshair lines
        surface.SetDrawColor(color)
        
        -- Top
        surface.DrawRect(x - thickness/2, y - gap - size, thickness, size)
        -- Bottom
        surface.DrawRect(x - thickness/2, y + gap, thickness, size)
        -- Left
        surface.DrawRect(x - gap - size, y - thickness/2, size, thickness)
        -- Right
        surface.DrawRect(x + gap, y - thickness/2, size, thickness)
    end

    -- Register the element
    local g_HudCrosshair = HudCrosshair:New()
    g_HudCrosshair:Init()

    -- ============================================================================
    --  HudDamageIndicator - Shows direction of damage
    -- ============================================================================

    local HudDamageIndicator = setmetatable({}, { __index = CHudElement })
    HudDamageIndicator.__index = HudDamageIndicator

    function HudDamageIndicator:New()
        local obj = CHudElement.New(self, "CHudDamageIndicator")
        setmetatable(obj, self)
        
        obj.m_DamageIndicators = {}
        obj:SetHiddenBits(HIDEHUD_HEALTH + HIDEHUD_PLAYERDEAD)
        
        return obj
    end

    function HudDamageIndicator:Init()
        self.m_DamageIndicators = {}
    end

    function HudDamageIndicator:AddDamage(attacker, damage)
        local ply = LocalPlayer()
        if not IsValid(ply) then return end
        
        -- Calculate angle to attacker
        local angle = 0
        if IsValid(attacker) and (attacker:IsPlayer() or attacker:IsNPC()) then
            local vecDir = (attacker:GetPos() - ply:GetPos()):GetNormalized()
            local forward = ply:GetForward()
            local right = ply:GetRight()
            
            -- Calculate angle
            angle = math.deg(math.atan2(vecDir:Dot(right), vecDir:Dot(forward)))
        end
        
        -- Add damage indicator
        table.insert(self.m_DamageIndicators, {
            angle = angle,
            time = CurTime(),
            damage = damage
        })
    end

    function HudDamageIndicator:Paint()
        if not self:ShouldDraw() then return end
        
        local ply = LocalPlayer()
        if not IsValid(ply) then return end
        
        local cx = ScrW() / 2
        local cy = ScrH() / 2
        local radius = 100
        
        -- Draw damage indicators
        for i = #self.m_DamageIndicators, 1, -1 do
            local indicator = self.m_DamageIndicators[i]
            local timeSince = CurTime() - indicator.time
            
            -- Remove old indicators
            if timeSince > 2 then
                table.remove(self.m_DamageIndicators, i)
            else
                -- Calculate alpha (fade out)
                local alpha = 255 * (1 - timeSince / 2)
                
                -- Calculate position
                local rad = math.rad(indicator.angle)
                local x = cx + math.sin(rad) * radius
                local y = cy - math.cos(rad) * radius
                
                -- Draw indicator
                local size = 16
                local color = Color(255, 0, 0, alpha)
                
                draw.NoTexture()
                surface.SetDrawColor(color)
                
                -- Draw arrow pointing to damage source
                local points = {
                    { x = x, y = y - size },
                    { x = x - size/2, y = y + size/2 },
                    { x = x + size/2, y = y + size/2 }
                }
                
                -- Rotate points
                for _, point in ipairs(points) do
                    local dx = point.x - x
                    local dy = point.y - y
                    point.x = x + dx * math.cos(rad) - dy * math.sin(rad)
                    point.y = y + dx * math.sin(rad) + dy * math.cos(rad)
                end
                
                surface.DrawPoly(points)
            end
        end
    end

    -- Register the element
    local g_HudDamageIndicator = HudDamageIndicator:New()
    g_HudDamageIndicator:Init()

    -- Hook for damage events (client-side)
    hook.Add("PlayerHurt", "NativeHUD_DamageIndicator", function(victim, attacker, healthRemaining, damageTaken)
        if victim == LocalPlayer() and IsValid(g_HudDamageIndicator) then
            g_HudDamageIndicator:AddDamage(attacker, damageTaken)
        end
    end)

    -- ============================================================================
    --  Main HUD Paint Hook
    -- ============================================================================

    hook.Add("HUDPaint", "NativeHUD_Paint", function()
        -- Paint all registered CHudElements
        for name, elem in pairs(CHudElement.Registry) do
            if elem.Paint then
                elem:Paint()
            end
        end
    end)

    -- ============================================================================
    --  Hide default GMod HUD elements
    -- ============================================================================

    local hideElements = {
        CHudHealth = true,
        CHudBattery = true,
        CHudAmmo = true,
        CHudSecondaryAmmo = true,
    }

    hook.Add("HUDShouldDraw", "NativeHUD_HideDefault", function(name)
        if hideElements[name] then
            return false
        end
    end)

    print("[NativeHUD] Loaded - Native HUD elements initialized")
    print("[NativeHUD] Use 'chaos_hud_theme' to switch between HL2 and GMod themes")
end)
