if SERVER then return end

-- ============================================================================
--  Native HUD Demo
--  
--  Demonstrates the native HL2 HUD port with theme switching
-- ============================================================================

print("=======================================================")
print("          NATIVE HUD FRAMEWORK DEMO")
print("=======================================================")
print("")
print("This demo showcases the ported Source SDK 2013 HUD system")
print("")
print("FEATURES:")
print("  ✓ CHudElement lifecycle management")
print("  ✓ Native HL2 HUD elements (Health, Suit, Ammo, etc.)")
print("  ✓ Theme system (HL2 and GMod themes)")
print("  ✓ Fully extensible framework")
print("  ✓ Backward compatible with ChaosHUD")
print("")
print("CONSOLE COMMANDS:")
print("  chaos_hud_theme hl2    - Switch to Half-Life 2 theme")
print("  chaos_hud_theme gmod   - Switch to Garry's Mod theme")
print("  chaos_hud_theme        - List available themes")
print("  chaos_hud_settings     - Open settings menu")
print("  chaos_hud_reload       - Reload current theme")
print("")
print("CURRENT THEME: " .. (HudTheme and HudTheme.GetCurrent().Name or "Loading..."))
print("")
print("The HUD will automatically display:")
print("  - Health (bottom left)")
print("  - Suit/Armor (bottom left, when you have armor)")
print("  - Ammo (bottom right, when holding a weapon)")
print("  - Aux Power (above health, when using sprint/flashlight)")
print("  - Crosshair (center)")
print("  - Damage indicators (around crosshair when hit)")
print("")
print("Try switching themes to see the differences!")
print("=======================================================")

-- Demo: Cycle through themes automatically
local themeCycle = {
    { name = "hl2", duration = 10 },
    { name = "gmod", duration = 10 }
}

local currentThemeIndex = 1
local nextThemeSwitch = CurTime() + themeCycle[1].duration

-- Create a notification system
local notifications = {}

local function AddNotification(text, color)
    table.insert(notifications, {
        text = text,
        color = color or Color(255, 255, 255),
        time = CurTime(),
        duration = 5
    })
end

hook.Add("HUDPaint", "NativeHUDDemo_Notifications", function()
    local y = 200
    
    for i = #notifications, 1, -1 do
        local notif = notifications[i]
        local timeSince = CurTime() - notif.time
        
        if timeSince > notif.duration then
            table.remove(notifications, i)
        else
            -- Fade in/out
            local alpha = 255
            if timeSince < 0.5 then
                alpha = 255 * (timeSince / 0.5)
            elseif timeSince > notif.duration - 0.5 then
                alpha = 255 * ((notif.duration - timeSince) / 0.5)
            end
            
            local col = Color(notif.color.r, notif.color.g, notif.color.b, alpha)
            draw.SimpleText(notif.text, "DermaLarge", ScrW() / 2, y, col, TEXT_ALIGN_CENTER)
            y = y + 30
        end
    end
    
    -- Show current theme info
    if HudTheme then
        local theme = HudTheme.GetCurrent()
        draw.SimpleText("Current Theme: " .. theme.Name, "DermaDefault", 10, 10, Color(255, 255, 255, 200))
        draw.SimpleText(theme.Description, "DermaDefault", 10, 30, Color(200, 200, 200, 200))
    end
end)

-- Demo: Auto theme switching (optional, can be disabled)
concommand.Add("chaos_hud_demo_cycle", function(ply, cmd, args)
    local enabled = tobool(args[1])
    
    if enabled == nil then
        enabled = true
    end
    
    if enabled then
        timer.Create("NativeHUDDemo_ThemeCycle", 1, 0, function()
            if CurTime() >= nextThemeSwitch then
                currentThemeIndex = currentThemeIndex % #themeCycle + 1
                local theme = themeCycle[currentThemeIndex]
                
                HudTheme.SetTheme(theme.name)
                AddNotification("Switched to " .. HudTheme.GetCurrent().Name .. " theme", Color(100, 255, 100))
                
                nextThemeSwitch = CurTime() + theme.duration
            end
        end)
        
        print("[Demo] Theme cycling enabled (10 seconds per theme)")
    else
        timer.Remove("NativeHUDDemo_ThemeCycle")
        print("[Demo] Theme cycling disabled")
    end
end)

-- Initial notification
timer.Simple(1, function()
    AddNotification("Native HUD Framework Demo Loaded!", Color(100, 255, 100))
    AddNotification("Type 'chaos_hud_theme' to switch themes", Color(255, 255, 100))
end)

-- Show help on F1
hook.Add("PlayerButtonDown", "NativeHUDDemo_Help", function(ply, button)
    if button == KEY_F1 then
        print("")
        print("=== HUD DEMO HELP ===")
        print("chaos_hud_theme hl2       - Switch to HL2 theme")
        print("chaos_hud_theme gmod      - Switch to GMod theme")
        print("chaos_hud_settings        - Open settings menu")
        print("chaos_hud_demo_cycle 1    - Enable auto theme cycling")
        print("chaos_hud_demo_cycle 0    - Disable auto theme cycling")
        print("=====================")
    end
end)

print("TIP: Press F1 for help")
print("TIP: Type 'chaos_hud_demo_cycle 1' to auto-cycle through themes")
