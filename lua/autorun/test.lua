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

    print("[AIChaos] HUD Test Initialized.")
end)
