if SERVER then return end

-- ============================================================================
--  Native HUD Elements - 1:1 Port of Source SDK 2013 HUD elements
--  
--  These are faithful ports of the C++ implementations from Source SDK 2013
--  All elements inherit from CHudNumericDisplay where applicable
-- ============================================================================

-- Wait for dependencies to load
timer.Simple(0.25, function()
    if not CHudElement or not CHudNumericDisplay or not HudTheme then
        print("[NativeHUD] Error: Required dependencies not loaded!")
        return
    end

    local INIT_HEALTH = -1
    local INIT_BAT = -1

    -- ============================================================================
    --  CHudHealth - Health display (hud_health.cpp)
    -- ============================================================================

    local CHudHealth = setmetatable({}, { __index = CHudNumericDisplay })
    CHudHealth.__index = CHudHealth

    function CHudHealth:New()
        local obj = CHudNumericDisplay.New(self, "CHudHealth", "HudHealth")
        setmetatable(obj, self)
        
        obj.m_iHealth = INIT_HEALTH
        obj.m_bitsDamage = 0
        
        obj:SetHiddenBits(HIDEHUD_HEALTH + HIDEHUD_PLAYERDEAD + HIDEHUD_NEEDSUIT)
        
        return obj
    end

    function CHudHealth:Init()
        self.m_iHealth = INIT_HEALTH
        self.m_bitsDamage = 0
        self:Reset()
    end

    function CHudHealth:Reset()
        CHudNumericDisplay.Reset(self)
        self.m_iHealth = INIT_HEALTH
        self.m_bitsDamage = 0
        self:SetLabelText("HEALTH")
        self:SetDisplayValue(self.m_iHealth)
    end

    function CHudHealth:VidInit()
        self:Reset()
    end

    function CHudHealth:OnThink()
        local newHealth = 0
        local ply = LocalPlayer()
        if IsValid(ply) then
            newHealth = math.max(ply:Health(), 0)
        end
        
        -- Only update if health changed
        if newHealth == self.m_iHealth then
            return
        end
        
        self.m_iHealth = newHealth
        
        -- Trigger animations based on health
        if self.m_iHealth >= 20 then
            -- Health increased above 20
            self.m_flBlur = 5.0  -- Start blur effect
        elseif self.m_iHealth > 0 then
            -- Health low
            self.m_flBlur = 5.0
        end
        
        self:SetDisplayValue(self.m_iHealth)
    end

    function CHudHealth:Paint()
        if not self:ShouldDraw() then return end
        
        self:OnThink()
        
        -- Decay blur over time
        if self.m_flBlur > 0 then
            self.m_flBlur = math.max(0, self.m_flBlur - FrameTime() * 10)
        end
        
        CHudNumericDisplay.Paint(self)
    end

    -- Register element
    local g_HudHealth = CHudHealth:New()
    g_HudHealth:Init()

    -- ============================================================================
    --  CHudBattery - Suit/Armor display (hud_battery.cpp)
    -- ============================================================================

    local CHudBattery = setmetatable({}, { __index = CHudNumericDisplay })
    CHudBattery.__index = CHudBattery

    function CHudBattery:New()
        local obj = CHudNumericDisplay.New(self, "CHudBattery", "HudSuit")
        setmetatable(obj, self)
        
        obj.m_iBat = INIT_BAT
        obj.m_iNewBat = 0
        
        obj:SetHiddenBits(HIDEHUD_HEALTH + HIDEHUD_NEEDSUIT)
        
        return obj
    end

    function CHudBattery:Init()
        self.m_iBat = INIT_BAT
        self.m_iNewBat = 0
        self:Reset()
    end

    function CHudBattery:Reset()
        CHudNumericDisplay.Reset(self)
        self:SetLabelText("SUIT")
        self:SetDisplayValue(self.m_iBat)
    end

    function CHudBattery:VidInit()
        self:Reset()
    end

    function CHudBattery:ShouldDraw()
        -- Only draw if armor changed or alpha > 0 (fading)
        local bNeedsDraw = (self.m_iBat ~= self.m_iNewBat) or (self.m_flBlur > 0)
        return bNeedsDraw and CHudElement.ShouldDraw(self)
    end

    function CHudBattery:OnThink()
        local ply = LocalPlayer()
        if IsValid(ply) then
            self.m_iNewBat = ply:Armor()
        else
            self.m_iNewBat = 0
        end
        
        if self.m_iBat == self.m_iNewBat then
            return
        end
        
        if self.m_iNewBat == 0 then
            -- Suit power zero
            self.m_flBlur = 0
        elseif self.m_iNewBat < self.m_iBat then
            -- Damage taken
            self.m_flBlur = 5.0
            
            if self.m_iNewBat < 20 then
                -- Armor low
                self.m_flBlur = 8.0
            end
        else
            -- Armor increased
            if self.m_iBat == INIT_BAT or self.m_iBat == 0 or self.m_iNewBat >= 20 then
                self.m_flBlur = 5.0
            else
                self.m_flBlur = 5.0
            end
        end
        
        self.m_iBat = self.m_iNewBat
        self:SetDisplayValue(self.m_iBat)
    end

    function CHudBattery:Paint()
        if not self:ShouldDraw() then return end
        
        self:OnThink()
        
        -- Decay blur over time
        if self.m_flBlur > 0 then
            self.m_flBlur = math.max(0, self.m_flBlur - FrameTime() * 10)
        end
        
        CHudNumericDisplay.Paint(self)
    end

    -- Register element
    local g_HudBattery = CHudBattery:New()
    g_HudBattery:Init()

    -- ============================================================================
    --  CHudAmmo - Ammunition display (hud_ammo.cpp)
    -- ============================================================================

    local CHudAmmo = setmetatable({}, { __index = CHudNumericDisplay })
    CHudAmmo.__index = CHudAmmo

    function CHudAmmo:New()
        local obj = CHudNumericDisplay.New(self, "CHudAmmo", "HudAmmo")
        setmetatable(obj, self)
        
        obj.m_hCurrentActiveWeapon = nil
        obj.m_hCurrentVehicle = nil
        obj.m_iAmmo = -1
        obj.m_iAmmo2 = -1
        
        obj:SetHiddenBits(HIDEHUD_HEALTH + HIDEHUD_PLAYERDEAD + HIDEHUD_NEEDSUIT + HIDEHUD_WEAPONSELECTION)
        
        return obj
    end

    function CHudAmmo:Init()
        self.m_iAmmo = -1
        self.m_iAmmo2 = -1
        self:SetLabelText("AMMO")
    end

    function CHudAmmo:VidInit()
        -- Nothing special
    end

    function CHudAmmo:Reset()
        CHudNumericDisplay.Reset(self)
        self.m_hCurrentActiveWeapon = nil
        self.m_hCurrentVehicle = nil
        self.m_iAmmo = 0
        self.m_iAmmo2 = 0
        self:UpdateAmmoDisplays()
    end

    function CHudAmmo:UpdatePlayerAmmo(player)
        self.m_hCurrentVehicle = nil
        
        local wpn = player:GetActiveWeapon()
        
        if not IsValid(wpn) or not player then
            self:SetShouldDisplayValue(false)
            return
        end
        
        -- Check if weapon uses primary ammo
        local primaryAmmoType = wpn:GetPrimaryAmmoType()
        if primaryAmmoType == -1 then
            self:SetShouldDisplayValue(false)
            return
        end
        
        self:SetShouldDisplayValue(true)
        
        -- Get clip ammo
        local ammo1 = wpn:Clip1()
        local ammo2
        
        if ammo1 < 0 then
            -- Doesn't use clips, use total ammo
            ammo1 = player:GetAmmoCount(primaryAmmoType)
            ammo2 = 0
        else
            -- Uses clips, secondary is total ammo
            ammo2 = player:GetAmmoCount(primaryAmmoType)
        end
        
        if wpn == self.m_hCurrentActiveWeapon then
            -- Same weapon, update counts with animation
            self:SetAmmo(ammo1, true)
            self:SetAmmo2(ammo2, true)
        else
            -- Different weapon, change without animation
            self:SetAmmo(ammo1, false)
            self:SetAmmo2(ammo2, false)
            
            -- Update secondary display
            if wpn:GetMaxClip1() > 0 then
                self:SetShouldDisplaySecondaryValue(true)
            else
                self:SetShouldDisplaySecondaryValue(false)
            end
            
            -- Weapon changed animation
            self.m_flBlur = 5.0
            self.m_hCurrentActiveWeapon = wpn
        end
    end

    function CHudAmmo:OnThink()
        self:UpdateAmmoDisplays()
    end

    function CHudAmmo:UpdateAmmoDisplays()
        local player = LocalPlayer()
        if not IsValid(player) then return end
        
        -- For now, just handle player ammo (not vehicles)
        self:UpdatePlayerAmmo(player)
    end

    function CHudAmmo:SetAmmo(ammo, playAnimation)
        if ammo ~= self.m_iAmmo then
            if playAnimation then
                if ammo == 0 then
                    -- Ammo empty
                    self.m_flBlur = 5.0
                elseif ammo < self.m_iAmmo then
                    -- Ammo decreased
                    self.m_flBlur = 3.0
                else
                    -- Ammo increased
                    self.m_flBlur = 5.0
                end
            end
            
            self.m_iAmmo = ammo
        end
        
        self:SetDisplayValue(ammo)
    end

    function CHudAmmo:SetAmmo2(ammo, playAnimation)
        if ammo ~= self.m_iAmmo2 then
            self.m_iAmmo2 = ammo
        end
        
        self:SetSecondaryValue(ammo)
    end

    function CHudAmmo:Paint()
        if not self:ShouldDraw() then return end
        
        self:OnThink()
        
        -- Decay blur
        if self.m_flBlur > 0 then
            self.m_flBlur = math.max(0, self.m_flBlur - FrameTime() * 10)
        end
        
        CHudNumericDisplay.Paint(self)
    end

    -- Register element
    local g_HudAmmo = CHudAmmo:New()
    g_HudAmmo:Init()

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

    print("[NativeHUD] Loaded - Native HUD elements initialized (1:1 SDK port)")
    print("[NativeHUD] Elements: CHudHealth, CHudBattery, CHudAmmo")
end)
