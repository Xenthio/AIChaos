if SERVER then return end

-- ============================================================================
--  HUD Theme System
--  
--  Manages different HUD themes (HL2, GMod, etc.)
--  Allows switching between themes with different colors, fonts, and styling
-- ============================================================================

HudTheme = HudTheme or {}
HudTheme.Themes = HudTheme.Themes or {}
HudTheme.CurrentTheme = HudTheme.CurrentTheme or "hl2"

-- ============================================================================
--  HL2 Theme (Source SDK 2013)
-- ============================================================================

HudTheme.Themes.hl2 = {
    Name = "Half-Life 2",
    Description = "Original Half-Life 2 HUD theme from Source SDK 2013",
    
    Colors = {
        -- Primary HUD colors
        BrightFg = Color(255, 220, 0, 255),         -- Yellow text
        Normal = Color(255, 208, 64, 255),          -- Normal yellow
        Caution = Color(255, 48, 0, 255),           -- Red/caution
        
        -- Background colors
        BgColor = Color(0, 0, 0, 76),               -- Semi-transparent black
        BgDark = Color(0, 0, 0, 100),               -- Darker background
        
        -- Damage/warning colors
        DamagedBg = Color(180, 0, 0, 200),
        DamagedFg = Color(180, 0, 0, 230),
        BrightDamagedFg = Color(255, 0, 0, 255),
        
        -- Aux Power colors
        AuxPowerLow = Color(255, 0, 0, 220),
        AuxPowerHigh = Color(255, 220, 0, 220),
        AuxPowerDisabled = Color(255, 220, 0, 70),
        
        -- Misc
        White = Color(255, 255, 255, 255),
        Black = Color(0, 0, 0, 255),
        Blank = Color(0, 0, 0, 0),
    },
    
    Fonts = {
        -- Main HUD fonts (using Half-Life 2 font)
        Numbers = { name = "HalfLife2", tall = 32, weight = 0, additive = true, antialias = true },
        NumbersGlow = { name = "HalfLife2", tall = 32, weight = 0, blur = 4, additive = true, antialias = true },
        NumbersSmall = { name = "HalfLife2", tall = 16, weight = 1000, additive = true, antialias = true },
        
        -- Text fonts (HL2 uses 9px text, not 8px)
        Text = { name = "Verdana", tall = 9, weight = 900, additive = true, antialias = true },
        TextSmall = { name = "Verdana", tall = 7, weight = 700, additive = true, antialias = true },
    },
    
    Layout = {
        -- Corner radius for panels (HL2 has larger radius than GMod)
        CornerRadius = 10,
        AuxCornerRadius = 8,
        
        -- Spacing
        Gap = 22,           -- Gap between columns
        StackGap = 6,       -- Gap between vertical items
        
        -- Margins
        MarginX = 16,
        MarginY = 48,       -- From bottom of screen
        
        -- Element positions (from HudLayout.res)
        HudHealth = { xpos = 16, ypos = 432, wide = 102, tall = 36 },
        HudSuit = { xpos = 140, ypos = 432, wide = 108, tall = 36 },
        HudAmmo = { xpos = "r150", ypos = 432, wide = 136, tall = 36 },
        HudAmmoSecondary = { xpos = "r76", ypos = 432, wide = 60, tall = 36 },
        HudSuitPower = { xpos = 16, ypos = 396, wide = 102, tall = 26 },
        HudFlashlight = { xpos = 270, ypos = 444, wide = 36, tall = 24 },
    },
    
    AuxPower = {
        BarInsetX = 8,
        BarInsetY = 15,
        BarWidth = 92,
        BarHeight = 4,
        BarChunkWidth = 6,
        BarChunkGap = 3,
    }
}

-- ============================================================================
--  GMod Theme (Garry's Mod)
-- ============================================================================

HudTheme.Themes.gmod = {
    Name = "Garry's Mod",
    Description = "Garry's Mod HUD theme with slightly different styling",
    
    Colors = {
        -- Primary HUD colors (GMod uses similar but slightly different shades)
        BrightFg = Color(255, 220, 0, 255),
        Normal = Color(255, 208, 64, 255),
        Caution = Color(255, 48, 0, 255),
        
        -- Background colors (GMod uses slightly more opaque backgrounds)
        BgColor = Color(0, 0, 0, 80),               -- Slightly more opaque
        BgDark = Color(0, 0, 0, 110),
        
        -- Damage/warning colors
        DamagedBg = Color(180, 0, 0, 200),
        DamagedFg = Color(180, 0, 0, 230),
        BrightDamagedFg = Color(255, 0, 0, 255),
        
        -- Aux Power colors
        AuxPowerLow = Color(255, 0, 0, 220),
        AuxPowerHigh = Color(255, 220, 0, 220),
        AuxPowerDisabled = Color(255, 220, 0, 70),
        
        -- Misc
        White = Color(255, 255, 255, 255),
        Black = Color(46, 43, 42, 255),             -- GMod uses a softer black
        Blank = Color(0, 0, 0, 0),
    },
    
    Fonts = {
        -- GMod uses slightly larger fonts
        Numbers = { name = "HalfLife2", tall = 36, weight = 0, additive = true, antialias = true },
        NumbersGlow = { name = "HalfLife2", tall = 36, weight = 0, blur = 5, additive = true, antialias = true },
        NumbersSmall = { name = "HalfLife2", tall = 18, weight = 1000, additive = true, antialias = true },
        
        -- Text fonts (GMod uses 8px text)
        Text = { name = "Verdana", tall = 8, weight = 900, additive = true, antialias = true },
        TextSmall = { name = "Verdana", tall = 6, weight = 700, additive = true, antialias = true },
    },
    
    Layout = {
        -- GMod uses smaller rounded corners than HL2
        CornerRadius = 8,
        AuxCornerRadius = 6,
        
        -- Spacing (slightly wider gaps)
        Gap = 24,
        StackGap = 7,
        
        -- Margins
        MarginX = 16,
        MarginY = 48,
        
        -- Element positions (similar to HL2 but with adjustments)
        HudHealth = { xpos = 16, ypos = 432, wide = 102, tall = 36 },
        HudSuit = { xpos = 140, ypos = 432, wide = 108, tall = 36 },
        HudAmmo = { xpos = "r150", ypos = 432, wide = 136, tall = 36 },
        HudAmmoSecondary = { xpos = "r76", ypos = 432, wide = 60, tall = 36 },
        HudSuitPower = { xpos = 16, ypos = 396, wide = 102, tall = 26 },
        HudFlashlight = { xpos = 270, ypos = 444, wide = 36, tall = 24 },
    },
    
    AuxPower = {
        BarInsetX = 8,
        BarInsetY = 15,
        BarWidth = 92,
        BarHeight = 5,      -- Slightly taller bars
        BarChunkWidth = 7,  -- Slightly wider chunks
        BarChunkGap = 3,
    }
}

-- ============================================================================
--  Theme Management Functions
-- ============================================================================

function HudTheme.GetCurrent()
    return HudTheme.Themes[HudTheme.CurrentTheme]
end

function HudTheme.GetTheme(name)
    return HudTheme.Themes[name]
end

function HudTheme.SetTheme(name)
    if not HudTheme.Themes[name] then
        print("[HudTheme] Theme '" .. name .. "' not found!")
        return false
    end
    
    HudTheme.CurrentTheme = name
    HudTheme.ApplyTheme()
    
    print("[HudTheme] Switched to theme: " .. HudTheme.Themes[name].Name)
    return true
end

function HudTheme.ApplyTheme()
    local theme = HudTheme.GetCurrent()
    if not theme then return end
    
    -- Create fonts with scale
    local scale = ScrH() / 480
    
    for fontName, fontData in pairs(theme.Fonts) do
        local scaledData = table.Copy(fontData)
        -- Convert 'tall' to 'size' and scale appropriately
        scaledData.size = math.Round((scaledData.tall or 12) * scale)
        scaledData.tall = nil
        
        if scaledData.blur then
            scaledData.blursize = math.Round(scaledData.blur * scale)
            scaledData.blur = nil
        end
        
        -- Create both HudTheme_ and ChaosHUD_ versions for compatibility
        surface.CreateFont("HudTheme_" .. fontName, scaledData)
        surface.CreateFont("ChaosHUD_" .. fontName, scaledData)  -- For backward compatibility
    end
    
    -- Store colors and layout in HudResources for compatibility
    if HudResources then
        HudResources.Scheme.Colors = theme.Colors
        HudResources.Scheme.BaseSettings = theme.Layout
    end
    
    -- Update ChaosHUD if it exists (for backward compatibility)
    if ChaosHUD then
        ChaosHUD.Colors.Yellow = theme.Colors.BrightFg
        ChaosHUD.Colors.Red = theme.Colors.Caution
        ChaosHUD.Colors.BgStandard = theme.Colors.BgColor
        ChaosHUD.Colors.BgDark = theme.Colors.BgDark
        ChaosHUD.Styles.CornerRadius = theme.Layout.CornerRadius
        ChaosHUD.Styles.AuxCornerRadius = theme.Layout.AuxCornerRadius
        ChaosHUD.Styles.Gap = theme.Layout.Gap
        ChaosHUD.Styles.StackGap = theme.Layout.StackGap
    end
    
    -- Trigger VidInit for all HUD elements
    if CHudElement then
        CHudElement.VidInitAll()
    end
end

-- ============================================================================
--  Console Commands
-- ============================================================================

concommand.Add("chaos_hud_theme", function(ply, cmd, args)
    if #args == 0 then
        -- List available themes
        print("[HudTheme] Available themes:")
        for name, theme in pairs(HudTheme.Themes) do
            local current = (name == HudTheme.CurrentTheme) and " (current)" or ""
            print("  " .. name .. " - " .. theme.Name .. current)
            print("    " .. theme.Description)
        end
        return
    end
    
    local themeName = string.lower(args[1])
    HudTheme.SetTheme(themeName)
end)

concommand.Add("chaos_hud_reload", function()
    HudTheme.ApplyTheme()
    print("[HudTheme] Theme reloaded")
end)

-- ============================================================================
--  Initialize
-- ============================================================================

hook.Add("OnScreenSizeChanged", "HudTheme_UpdateFonts", function()
    HudTheme.ApplyTheme()
end)

-- Apply default theme on load
timer.Simple(0.1, function()
    HudTheme.ApplyTheme()
    print("[HudTheme] Loaded - Current theme: " .. HudTheme.GetCurrent().Name)
    print("[HudTheme] Use 'chaos_hud_theme <name>' to switch themes")
    print("[HudTheme] Use 'chaos_hud_theme' to list available themes")
end)
