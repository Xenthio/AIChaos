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
        local layout = theme.Layout.HudAmmo
        local x = HudResources.ConvertPosition(layout.xpos, ScrW())
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
    --  HudSuitPower - Aux Power display (Sprint, Flashlight, Oxygen)
    -- ============================================================================

    local HudSuitPower = setmetatable({}, { __index = CHudElement })
    HudSuitPower.__index = HudSuitPower

    function HudSuitPower:New()
        local obj = CHudElement.New(self, "CHudSuitPower")
        setmetatable(obj, self)
        
        obj.m_flSuitPower = 100
        obj:SetHiddenBits(HIDEHUD_HEALTH + HIDEHUD_PLAYERDEAD + HIDEHUD_NEEDSUIT)
        
        return obj
    end

    function HudSuitPower:Init()
        self.m_flSuitPower = 100
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

    function HudSuitPower:Paint()
        if not self:ShouldDraw() then return end
        
        local ply = LocalPlayer()
        if not IsValid(ply) then return end
        
        self.m_flSuitPower = ply:GetSuitPower()
        local activeItems = self:GetActiveItems()
        
        local scale = ChaosHUD.GetScale()
        local x = 16 * scale
        local y = ScrH() - (48 * scale) - (36 * scale) - (6 * scale)
        
        ChaosHUD.DrawAuxBar(x, y, "AUX", self.m_flSuitPower, activeItems)
    end

    -- Register the element
    local g_HudSuitPower = HudSuitPower:New()
    g_HudSuitPower:Init()

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
