# HUD Framework Documentation

## Overview

The AIChaos HUD framework is a complete port of the Source SDK 2013 HUD system to Garry's Mod Lua. It provides faithful recreations of Half-Life 2's HUD elements with full extensibility and theme support.

## Architecture

### Core Components

1. **CHudElement** (`cl_hud_element.lua`)
   - Base class for all HUD elements
   - Lifecycle management (Init, VidInit, LevelInit, Reset)
   - Visibility system with HIDEHUD flags
   - Render group management

2. **HudResources** (`cl_hud_resources.lua`)
   - Resource file parser for `.res` format
   - Position/dimension conversion helpers
   - Color and font management

3. **HudTheme** (`cl_hud_themes.lua`)
   - Theme system for HL2 and GMod styles
   - Color schemes, fonts, and layout definitions
   - Theme switching functionality

4. **Native HUD Elements** (`cl_native_hud_elements.lua`)
   - HudHealth - Player health display
   - HudSuit - Armor/suit power display
   - HudAmmo - Ammunition display
   - HudSuitPower - Aux power (sprint, flashlight, oxygen)
   - HudCrosshair - Crosshair display
   - HudDamageIndicator - Directional damage indicators

5. **ChaosHUD Framework** (`cl_chaos_hud_framework.lua`)
   - Layout system (HStack, VStack)
   - Drawing primitives
   - Animation system
   - Custom element support

## Creating Custom HUD Elements

### Using CHudElement

```lua
-- Create a new HUD element class
local MyHudElement = setmetatable({}, { __index = CHudElement })
MyHudElement.__index = MyHudElement

function MyHudElement:New()
    local obj = CHudElement.New(self, "MyHudElement")
    setmetatable(obj, self)
    
    -- Set which situations hide this element
    obj:SetHiddenBits(HIDEHUD_PLAYERDEAD)
    
    return obj
end

function MyHudElement:Init()
    -- Called when HUD initializes
    self.myData = 0
end

function MyHudElement:VidInit()
    -- Called when video mode changes
    -- Good place to recreate fonts/materials
end

function MyHudElement:Reset()
    -- Called when player respawns
    self.myData = 0
end

function MyHudElement:ShouldDraw()
    -- Return false to hide element
    if not CHudElement.ShouldDraw(self) then
        return false
    end
    
    -- Add custom visibility logic
    return true
end

function MyHudElement:Paint()
    if not self:ShouldDraw() then return end
    
    -- Draw your HUD element here
    draw.SimpleText("My HUD", "DermaDefault", 100, 100, Color(255, 255, 255))
end

-- Register the element
local g_MyHudElement = MyHudElement:New()
g_MyHudElement:Init()
```

### Using ChaosHUD Framework

```lua
-- Create a custom element object
local myElement = {}

function myElement:GetSize()
    return 200, 40  -- width, height
end

function myElement:Draw(x, y, h)
    -- h is the current animated height (for smooth show/hide)
    draw.RoundedBox(8, x, y, 200, h, Color(0, 0, 0, 100))
    draw.SimpleText("Custom", "DermaDefault", x + 10, y + 10, Color(255, 255, 255))
    return 200, 40
end

-- Add to a column
ChaosHUD.AddVStackElement("Health", "MyCustomElement", myElement, 50)

-- Or add a new column
ChaosHUD.RegisterColumn("MyColumn", 200, function() return true end, 100)
if ChaosHUD.HStackMap["MyColumn"] then
    ChaosHUD.HStackMap["MyColumn"].base_element = myElement
end
```

## Themes

### Switching Themes

```lua
-- Via console
chaos_hud_theme hl2    -- Switch to Half-Life 2 theme
chaos_hud_theme gmod   -- Switch to Garry's Mod theme

-- Via Lua
HudTheme.SetTheme("hl2")
HudTheme.SetTheme("gmod")

-- Open settings menu
chaos_hud_settings
```

### Theme Structure

```lua
HudTheme.Themes.mytheme = {
    Name = "My Theme",
    Description = "A custom theme",
    
    Colors = {
        BrightFg = Color(255, 220, 0, 255),
        Caution = Color(255, 48, 0, 255),
        BgColor = Color(0, 0, 0, 76),
        -- etc.
    },
    
    Fonts = {
        Numbers = { name = "HalfLife2", tall = 32, weight = 0 },
        Text = { name = "Verdana", tall = 8, weight = 900 },
    },
    
    Layout = {
        CornerRadius = 8,
        Gap = 22,
        StackGap = 6,
        -- Element positions
        HudHealth = { xpos = 16, ypos = 432, wide = 102, tall = 36 },
    }
}
```

## HIDEHUD Flags

These flags control when HUD elements are hidden:

- `HIDEHUD_WEAPONSELECTION` (1) - Hide weapon selection HUD
- `HIDEHUD_FLASHLIGHT` (2) - Hide flashlight HUD
- `HIDEHUD_ALL` (4) - Hide all HUD
- `HIDEHUD_HEALTH` (8) - Hide health/armor
- `HIDEHUD_PLAYERDEAD` (16) - Hide when dead
- `HIDEHUD_NEEDSUIT` (32) - Hide when no HEV suit
- `HIDEHUD_MISCSTATUS` (64) - Hide misc status
- `HIDEHUD_CHAT` (128) - Hide chat
- `HIDEHUD_CROSSHAIR` (256) - Hide crosshair
- `HIDEHUD_VEHICLE_CROSSHAIR` (512) - Hide vehicle crosshair
- `HIDEHUD_INVEHICLE` (1024) - Hide when in vehicle
- `HIDEHUD_BONUS_PROGRESS` (2048) - Hide bonus progress

## Console Commands

- `chaos_hud_theme [name]` - Switch theme or list available themes
- `chaos_hud_reload` - Reload current theme
- `chaos_hud_settings` - Open settings menu

## Differences from Source SDK

1. **Lua vs C++**: All elements are in Lua, allowing runtime modification
2. **No VGUI**: Uses GMod's drawing primitives instead of VGUI panels
3. **Simplified Events**: Uses hooks instead of game events
4. **Extended Layout**: Additional layout areas (TopLeft, TopRight, Center)

## Compatibility

- **GMod Versions**: Tested on latest stable GMod
- **Source SDK**: Based on Source SDK 2013 MP branch
- **Backward Compatible**: Works alongside existing ChaosHUD test code

## Performance

- All elements use efficient drawing primitives
- Animations use frame-time based interpolation
- Fonts are cached and scaled appropriately
- Minimal garbage collection pressure

## Extensibility

The framework is designed to be extended:

1. Add new themes to `HudTheme.Themes`
2. Create custom CHudElements with full lifecycle
3. Use ChaosHUD for flexible layout systems
4. Mix and match native and custom elements

## Examples

See `lua/tests/hudframeworktest.lua` for comprehensive examples of:
- Custom numeric displays
- Aux power bars
- 3D model rendering
- Custom animations
- Multi-column layouts
- Locator system

## Credits

- Original Source SDK 2013 by Valve Corporation
- Garry's Mod by Facepunch Studios
- Port by AIChaos development team
