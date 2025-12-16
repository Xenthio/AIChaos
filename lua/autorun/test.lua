if SERVER then return end

-- Cleanup old hooks from previous tests
local old_hooks = {
    "AIChaos_NativeHudTest",
    "AIChaos_Mirror_Test",
    "AIChaos_Mirror_Spin",
    "AIChaos_Flip_Test"
}
for _, h in ipairs(old_hooks) do
    hook.Remove("HUDPaint", h)
    hook.Remove("CreateMove", h)
end

-- Ensure Framework is loaded (in case of autorefresh order issues)
if not ChaosHUD then include("autorun/client/cl_chaos_hud_framework.lua") end

-- Delay initialization to ensure framework is ready
timer.Simple(0.1, function()
    print("[AIChaos] Initializing HUD Framework Test...")

    -- 1. Register a new Column for our custom elements
    -- Priority: Health=10, Suit=20. We want this to be 30 (to the right).
    ChaosHUD.RegisterColumn("Chaos", 102, function() return true end, 30)

    -- 2. Create the Base Element (The Numeric Display)
    -- We ll use a closure to hold the test state
    local test_state = {
        val = 100,
        aux_val = 100,
        next_update = 0
    }

    local base_elem = ChaosHUD.CreateNumericElement("CHAOS", function()
        -- Update random values periodically
        if CurTime() > test_state.next_update then
            test_state.val = math.random(0, 30)
            test_state.aux_val = math.random(0, 40)
            test_state.next_update = CurTime() + 4
        end
        return test_state.val
    end, { warn_low = true, low_threshold = 20 })

    -- Assign it as the base of our column
    if ChaosHUD.HStackMap["Chaos"] then
        ChaosHUD.HStackMap["Chaos"].base_element = base_elem
    end

    -- 3. Add Vertical Stack Elements (Aux Bars)

    -- Element 1: "CHAOS POWER" (Randomized)
    local aux1 = ChaosHUD.CreateAuxElement("CHAOS POWER", function()
        return test_state.aux_val
    end, function()
        local items = {}
        if LocalPlayer():FlashlightIsOn() then table.insert(items, "FLASHLIGHT") end
        if LocalPlayer():IsSprinting() then table.insert(items, "SPRINT") end
        return items
    end)

    ChaosHUD.AddVStackElement("Chaos", "ChaosPower", aux1, 10)

    -- Element 2: "SPUNK METER" (Static)
    local aux2 = ChaosHUD.CreateAuxElement("SPUNK METER", function()
        return 80
    end, nil)

    ChaosHUD.AddVStackElement("Chaos", "SpunkMeter", aux2, 20)

    -- 4. Test adding something to the Vanilla "Health" stack too!
    -- Let s add a small bar above Health just to prove we can.
    local health_aux = ChaosHUD.CreateAuxElement("VITALS", function()
        return LocalPlayer():Health()
    end, nil)

    ChaosHUD.AddVStackElement("Health", "VitalsAux", health_aux, 10)

    -- 5. Test adding another custom column to the HStack
    -- This demonstrates adding multiple independent columns
    ChaosHUD.RegisterColumn("Extra", 102, function() return true end, 40)
    
    local extra_state = { val = 42, next_update = 0 }
    local extra_base = ChaosHUD.CreateNumericElement("EXTRA", function()
        if CurTime() > extra_state.next_update then
            extra_state.val = math.random(0, 100)
            extra_state.next_update = CurTime() + 3
        end
        return extra_state.val
    end, { warn_low = false })
    
    if ChaosHUD.HStackMap["Extra"] then
        ChaosHUD.HStackMap["Extra"].base_element = extra_base
    end

    -- 6. Test adding a completely custom element (implementing the interface manually)
    -- This shows how to make unique UI elements that still fit the framework
    local custom_elem = {}
    function custom_elem:GetSize()
        return 102 * ChaosHUD.GetScale(), 40 * ChaosHUD.GetScale()
    end

    function custom_elem:Draw(x, y, h)
        local scale = ChaosHUD.GetScale()
        local w = 102 * scale
        
        -- Draw Background using framework style
        draw.RoundedBox(ChaosHUD.Styles.CornerRadius, x, y, w, h, ChaosHUD.Colors.BgStandard)
        
        -- Draw Label
        surface.SetFont("ChaosHUD_Text")
        surface.SetTextColor(ChaosHUD.Colors.Yellow)
        surface.SetTextPos(x + (8 * scale), y + (4 * scale))
        surface.DrawText("WAVEFORM")
        
        -- Draw Custom Content (Sine Wave)
        surface.SetDrawColor(ChaosHUD.Colors.Yellow)
        local center_y = y + (24 * scale)
        local prev_x, prev_y
        
        -- Draw a little oscillating line
        for i = 0, 20 do
            local wave_x = x + (10 * scale) + (i * 4 * scale)
            local wave_y = center_y + math.sin(CurTime() * 5 + i * 0.5) * (8 * scale)
            
            if prev_x then
                surface.DrawLine(prev_x, prev_y, wave_x, wave_y)
            end
            
            prev_x = wave_x
            prev_y = wave_y
        end
    end

    ChaosHUD.AddVStackElement("Extra", "CustomWave", custom_elem, 10)

    -- 7. Test adding a 3D Model element (Spinning Gnome)
    -- We want this to be its own column in the HStack
    ChaosHUD.RegisterColumn("GnomeCol", 102, function() return true end, 50)

    if IsValid(AIChaos_GnomeModel) then AIChaos_GnomeModel:Remove() end
    
    local gnome_elem = {}
    function gnome_elem:GetSize()
        return 102 * ChaosHUD.GetScale(), 80 * ChaosHUD.GetScale()
    end

    function gnome_elem:Draw(x, y, h)
        local scale = ChaosHUD.GetScale()
        local w = 102 * scale
        h = h or (80 * scale) -- Default height if not provided (e.g. base element)
        
        -- Align bottom with standard elements (36 units high)
        local draw_y = y + (36 * scale) - h

        -- Draw Background
        draw.RoundedBox(ChaosHUD.Styles.CornerRadius, x, draw_y, w, h, ChaosHUD.Colors.BgStandard)
        
        -- Draw Label
        surface.SetFont("ChaosHUD_Text")
        surface.SetTextColor(ChaosHUD.Colors.Yellow)
        surface.SetTextPos(x + (8 * scale), draw_y + (4 * scale))
        surface.DrawText("GNOME")
        
        -- Manage Model
        if not IsValid(AIChaos_GnomeModel) then
            AIChaos_GnomeModel = ClientsideModel("models/props_junk/gnome.mdl", RENDERGROUP_OTHER)
            AIChaos_GnomeModel:SetNoDraw(true)
        end
        
        local mdl = AIChaos_GnomeModel
        local spin = CurTime() * 90
        mdl:SetAngles(Angle(0, spin, 0))
        
        -- Render 3D Viewport
        local view_x, view_y = x + (2 * scale), draw_y + (14 * scale)
        local view_w, view_h = w - (4 * scale), h - (16 * scale)
        
        local cam_pos = Vector(35, 0, 20)
        local cam_ang = (Vector(0,0,10) - cam_pos):Angle()
        
        cam.Start3D(cam_pos, cam_ang, 70, view_x, view_y, view_w, view_h)
            render.SuppressEngineLighting(true)
            render.ResetModelLighting(1,1,1)
            render.SetColorModulation(1,1,1)
            render.SetBlend(1)
            
            mdl:DrawModel()
            
            render.SuppressEngineLighting(false)
        cam.End3D()

        return w, h
    end

    if ChaosHUD.HStackMap["GnomeCol"] then
        ChaosHUD.HStackMap["GnomeCol"].base_element = gnome_elem
    end


    -- 9. Test adding a custom Ammo Counter above the native one
    local custom_ammo = ChaosHUD.CreateAmmoElement("CUSTOM", function()
        local ply = LocalPlayer()
        local wpn = ply:GetActiveWeapon()
        if IsValid(wpn) then return wpn:Clip1() end
        return 0
    end, function()
        local ply = LocalPlayer()
        local wpn = ply:GetActiveWeapon()
        if IsValid(wpn) then return ply:GetAmmoCount(wpn:GetPrimaryAmmoType()) end
        return nil
    end)

    ChaosHUD.AddRightVStackElement("Ammo", "CustomAmmo", custom_ammo, 20)

    -- 10. WACKY TEST: Speedometer Column
    -- Always show column so Wanted level can persist
    ChaosHUD.RegisterColumn("Speed", 102, function() return true end, 50)

    local speed_real = ChaosHUD.CreateNumericElement("VELOCITY", function()
        return math.Round(LocalPlayer():GetVelocity():Length())
    end, { warn_low = false })

    -- Proxy to hide Velocity when not moving, but keep column alive
    local speed_proxy = {}
    function speed_proxy:Draw(x, y)
        if LocalPlayer():InVehicle() or LocalPlayer():GetVelocity():Length() > 100 then
            return speed_real:Draw(x, y)
        else
            -- Hidden, but reserve width so column doesn't collapse
            return 102 * ChaosHUD.GetScale(), 0
        end
    end

    if ChaosHUD.HStackMap["Speed"] then
        ChaosHUD.HStackMap["Speed"].base_element = speed_proxy
    end

    -- 11. WACKY TEST: "Wanted Level" (Custom Star Renderer)
    local wanted_elem = {}
    function wanted_elem:GetSize()
        return 102 * ChaosHUD.GetScale(), 30 * ChaosHUD.GetScale()
    end
    function wanted_elem:Draw(x, y, h)
        local scale = ChaosHUD.GetScale()
        h = h or (30 * scale)
        local draw_y = y
        
        -- Draw BG
        draw.RoundedBox(ChaosHUD.Styles.CornerRadius, x, draw_y, 102 * scale, h, ChaosHUD.Colors.BgStandard)
        
        -- Draw Label
        surface.SetFont("ChaosHUD_Text")
        surface.SetTextColor(ChaosHUD.Colors.Yellow)
        surface.SetTextPos(x + (8 * scale), draw_y + (4 * scale))
        surface.DrawText("WANTED")
        
        -- Draw Stars
        local star_size = 16 * scale
        local start_x = x + (10 * scale)
        local star_y = draw_y + (12 * scale)
        local level = math.floor(CurTime() % 6) -- Cycle 0-5 stars
        
        for i = 1, 5 do
            local sx = start_x + ((i-1) * (18 * scale))
            local col = (i <= level) and ChaosHUD.Colors.Yellow or Color(50, 50, 50, 200)
            
            draw.NoTexture()
            surface.SetDrawColor(col)
            -- Simple diamond shape for star
            local poly = {
                { x = sx + star_size/2, y = star_y },
                { x = sx + star_size, y = star_y + star_size/2 },
                { x = sx + star_size/2, y = star_y + star_size },
                { x = sx, y = star_y + star_size/2 }
            }
            surface.DrawPoly(poly)
        end
        
        return 102 * scale, h
    end
    
    ChaosHUD.AddVStackElement("Speed", "WantedLevel", wanted_elem, 10)

    -- 12. WACKY TEST: Compass (Text Based)
    local compass_elem = {}
    function compass_elem:GetSize() return 102 * ChaosHUD.GetScale(), 24 * ChaosHUD.GetScale() end
    function compass_elem:Draw(x, y, h)
        local scale = ChaosHUD.GetScale()
        h = h or (24 * scale)
        local draw_y = y
        
        draw.RoundedBox(ChaosHUD.Styles.CornerRadius, x, draw_y, 102 * scale, h, ChaosHUD.Colors.BgStandard)
        
        local ang = LocalPlayer():EyeAngles().y
        local dirs = {"N", "NE", "E", "SE", "S", "SW", "W", "NW"}
        local idx = math.Round( ((ang + 180) % 360) / 45 )
        if idx == 0 then idx = 8 end -- Fix wrap
        -- Actually (ang + 180) / 45 -> 0..8. 
        -- Let's just do simple math
        -- 0 = N (in Source? No, 0 is East usually, 90 North)
        -- Source: 0=East, 90=North, 180=West, -90=South
        
        local dir_str = "N"
        if ang > -22.5 and ang <= 22.5 then dir_str = "E"
        elseif ang > 22.5 and ang <= 67.5 then dir_str = "NE"
        elseif ang > 67.5 and ang <= 112.5 then dir_str = "N"
        elseif ang > 112.5 and ang <= 157.5 then dir_str = "NW"
        elseif ang > 157.5 or ang <= -157.5 then dir_str = "W"
        elseif ang > -157.5 and ang <= -112.5 then dir_str = "SW"
        elseif ang > -112.5 and ang <= -67.5 then dir_str = "S"
        elseif ang > -67.5 and ang <= -22.5 then dir_str = "SE"
        end
        
        surface.SetFont("ChaosHUD_Text")
        surface.SetTextColor(ChaosHUD.Colors.Yellow)
        local w, _ = surface.GetTextSize(dir_str)
        surface.SetTextPos(x + (51 * scale) - (w/2), draw_y + (4 * scale))
        surface.DrawText(dir_str)
        
        return 102 * scale, h
    end
    
    ChaosHUD.AddVStackElement("Health", "Compass", compass_elem, 5) -- Very top of Health stack

    print("[AIChaos] HUD Test Initialized.")
end)
