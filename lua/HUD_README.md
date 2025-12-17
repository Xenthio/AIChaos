# Half-Life 2 HUD Framework

A complete port of Source SDK 2013's HUD system to Garry's Mod Lua, providing faithful recreations of Half-Life 2's HUD elements with full extensibility.

## Features

✅ **Full CHudElement Port**
- Complete lifecycle management (Init, VidInit, LevelInit, Reset)
- Visibility system with HIDEHUD flags
- Render group management
- Automatic hook integration

✅ **Native HUD Elements**
- HudHealth - Player health with low health warning
- HudSuit - Armor display
- HudAmmo - Primary and reserve ammunition
- HudSuitPower - Aux power (sprint, flashlight, oxygen)
- HudCrosshair - Simple HL2-style crosshair
- HudDamageIndicator - Directional damage indicators

✅ **Theme System**
- **HL2 Theme** - Faithful to Source SDK 2013 Half-Life 2
- **GMod Theme** - Garry's Mod styling with tweaked fonts/colors
- Easy theme switching via console or settings menu
- Extensible - add your own themes

✅ **Resource System**
- Parse HudLayout.res for positioning
- Parse ClientScheme.res for colors/fonts
- Position helpers (r150, c-100, f0)
- Automatic scaling for different resolutions

✅ **Backward Compatible**
- Works alongside existing ChaosHUD framework
- All existing test code remains functional
- Same extensibility as before

## Quick Start

### Switching Themes

```
// In console
chaos_hud_theme hl2    // Switch to Half-Life 2 theme
chaos_hud_theme gmod   // Switch to Garry's Mod theme
chaos_hud_settings     // Open settings menu
```

### Creating Custom Elements

```lua
-- Using CHudElement (recommended)
local MyElement = setmetatable({}, { __index = CHudElement })
MyElement.__index = MyElement

function MyElement:New()
    local obj = CHudElement.New(self, "MyElement")
    setmetatable(obj, self)
    obj:SetHiddenBits(HIDEHUD_PLAYERDEAD)
    return obj
end

function MyElement:Paint()
    if not self:ShouldDraw() then return end
    draw.SimpleText("My HUD", "DermaDefault", 100, 100, Color(255, 255, 255))
end

local g_MyElement = MyElement:New()
g_MyElement:Init()
```

## Files Structure

```
lua/autorun/client/
├── cl_hud_element.lua          # CHudElement base class
├── cl_hud_resources.lua        # Resource file parser
├── cl_hud_themes.lua           # Theme system (HL2, GMod)
├── cl_native_hud_elements.lua  # Native HUD elements
├── cl_hud_settings.lua         # Settings menu UI
└── cl_chaos_hud_framework.lua  # Original framework (compat)
```

## Console Commands

| Command | Description |
|---------|-------------|
| `chaos_hud_theme [name]` | Switch theme or list themes |
| `chaos_hud_reload` | Reload current theme |
| `chaos_hud_settings` | Open settings menu |

## Theme Comparison

| Feature | HL2 Theme | GMod Theme |
|---------|-----------|------------|
| Font Size | 32px | 36px |
| Corner Radius | 8px | 10px |
| Gap | 22px | 24px |
| Background Alpha | 76 | 80 |
| Bar Height | 4px | 5px |

## Documentation

See [HUD_DOCUMENTATION.md](HUD_DOCUMENTATION.md) for complete documentation including:
- Detailed API reference
- Custom element creation guide
- Theme creation guide
- Performance considerations
- Examples and best practices

## References

- [Source SDK 2013](https://github.com/ValveSoftware/source-sdk-2013) - Original C++ implementation
- [Garry's Mod](https://github.com/Facepunch/garrysmod) - Resource files and GMod specifics
- Half-Life 2 - Game design and visual style

## License

Based on Source SDK 2013, which is under the Source SDK License.
Port implementation and extensions are part of the AIChaos project.
