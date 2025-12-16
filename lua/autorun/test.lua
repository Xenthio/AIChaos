if SERVER then return end

-- Cleanup old hooks from previous tests just in case
local old_hooks = {
    "AIChaos_Mirror_Test",
    "AIChaos_Mirror_Spin",
    "AIChaos_Flip_Test"
}
for _, h in ipairs(old_hooks) do
    hook.Remove("CreateMove", h)
    hook.Remove("PostDrawEffects", h)
    hook.Remove("PreDrawViewModel", h)
    hook.Remove("PostDrawViewModel", h)
    hook.Remove("PreDrawPlayerHands", h)
    hook.Remove("PostDrawPlayerHands", h)
    hook.Remove("DrawPhysgunBeam", h)
    hook.Remove("FireAnimationEvent", h)
    hook.Remove("CalcViewModelView", h)
    hook.Remove("EntityFireBullets", h)
    hook.Remove("InputMouseApply", h)
end

-- Helper to calculate scale based on screen height (mimicking Source HUD scaling)
local function GetHudScale()
    return ScrH() / 480
end

local function UpdateFonts()
    local scale = GetHudScale()
    
    -- Define fonts to match HL2 HUD
    -- The "Halflife2" font family contains the iconic HUD digits
    surface.CreateFont( "AIChaos_HudNumbers", {
        font = "Halflife2",
        size = math.Round(32 * scale),
        weight = 0,
        antialias = true,
        additive = true,
    } )

    -- A blurry version for the glow effect
    surface.CreateFont( "AIChaos_HudNumbersGlow", {
        font = "Halflife2",
        size = math.Round(32 * scale),
        weight = 0,
        blursize  = math.Round(4 * scale),
        scanlines = math.Round(2 * scale),
        antialias = true,
        additive = true,
    } )

    -- The label text (e.g. "HEALTH", "SUIT")
    surface.CreateFont( "AIChaos_HudText", {
        font = "Verdana", -- Closest standard match to the small HUD labels
        size = math.Round(8 * scale),
        weight = 900,
        antialias = true,
        additive = true,
    } )
end

-- Initialize fonts
UpdateFonts()

-- Re-create fonts when resolution changes
hook.Add("OnScreenSizeChanged", "AIChaos_UpdateFonts", UpdateFonts)

local HUD_COLOR = Color(255, 220, 0, 255) -- The classic yellow/orange
local HUD_COLOR_LOW = Color(255, 0, 0, 255)
local HUD_COLOR_GLOW = Color(255, 220, 0, 128)
local HUD_COLOR_GLOW_LOW = Color(255, 0, 0, 128)

local function InterpLinear(t) return t end
local function InterpAccel(t) return t * t end
local function InterpDeaccel(t) return 1 - (1 - t) * (1 - t) end

local function DrawNativeHudElement(x, y, label, value, last_change_time, options)
    options = options or {}
    local warn_low = options.warn_low or false
    local low_threshold = options.low_threshold or 20
    
    local val_str = tostring(value)
    local scale = GetHudScale()
    
    -- Standard HL2 HUD Dimensions (Reference 640x480)
    -- From HudLayout.res: wide 102, tall 36
    local width = 102 * scale
    local height = 36 * scale
    
    -- Determine Colors
    local is_low = warn_low and (value <= low_threshold)
    local text_color = HUD_COLOR
    local glow_color = HUD_COLOR_GLOW
    local bg_color = Color(0, 0, 0, 72) -- Standard BgColor
    
    if is_low then
        -- Static Red for Text (matches FgColor in HealthLow, and user request for no pulse on text)
        text_color = Color(255, 0, 0, 255) -- DamagedFg

        -- Pulse Logic (HealthPulse)
        -- Loop duration: 0.8s
        local cycle = math.fmod(CurTime(), 0.8)
        
        -- Colors for Low Health Pulse
        local col_bg_base = Color(0, 0, 0, 72)            -- BgColor
        local col_bg_bright = Color(100, 0, 0, 80)        -- "100 0 0 80"
        
        if cycle < 0.1 then
            -- Phase 1: Attack (0.0 -> 0.1) - Linear
            local t = cycle / 0.1
            t = InterpLinear(t)
            
            bg_color = Color(
                Lerp(t, col_bg_base.r, col_bg_bright.r),
                Lerp(t, col_bg_base.g, col_bg_bright.g),
                Lerp(t, col_bg_base.b, col_bg_bright.b),
                Lerp(t, col_bg_base.a, col_bg_bright.a)
            )
        else
            -- Phase 2: Decay (0.1 -> 0.8) - Deaccel
            local t = (cycle - 0.1) / 0.7
            t = InterpDeaccel(t) -- Starts fast, ends slow
            
            bg_color = Color(
                Lerp(t, col_bg_bright.r, col_bg_base.r),
                Lerp(t, col_bg_bright.g, col_bg_base.g),
                Lerp(t, col_bg_bright.b, col_bg_base.b),
                Lerp(t, col_bg_bright.a, col_bg_base.a)
            )
        end
        
        -- Blur Pulse (matches text intensity roughly)
        -- Reference: Blur 5 -> 2.
        -- We simulate this by pulsing the alpha of the glow font.
        -- When bright (t=0 in decay), alpha high. When base (t=1 in decay), alpha low.
        local glow_alpha = 0
        if cycle < 0.1 then
             glow_alpha = Lerp(cycle/0.1, 100, 255)
        else
             local t = InterpDeaccel((cycle - 0.1) / 0.7)
             glow_alpha = Lerp(t, 255, 100)
        end
        glow_color = Color(255, 0, 0, glow_alpha)
    end

    -- Draw Background (Rounded Rectangle)
    draw.RoundedBox(8, x, y, width, height, bg_color)
    
    -- Measure Text
    surface.SetFont("AIChaos_HudNumbers")
    local w_val, h_val = surface.GetTextSize(val_str)
    local w_digit, _ = surface.GetTextSize("0") -- Width of a single digit for indentation
    
    surface.SetFont("AIChaos_HudText")
    local w_lbl, h_lbl = surface.GetTextSize(label)
    
    -- Position Label: Bottom Left of the box
    -- From HudLayout.res: text_xpos 8, text_ypos 20
    local lbl_x = x + (8 * scale)
    local lbl_y = y + (20 * scale)
    
    surface.SetFont("AIChaos_HudText")
    surface.SetTextColor(text_color)
    surface.SetTextPos(lbl_x, lbl_y)
    surface.DrawText(label)
    
    -- Position Number:
    -- From HudLayout.res: digit_xpos 50, digit_ypos 2
    -- Note: digit_xpos 50 is the starting point for the number.
    -- The C++ code logic shifts it based on value < 100 or < 10.
    
    local num_x = x + (50 * scale) 
    local num_y = y + (2 * scale)
    
    if value < 100 then num_x = num_x + w_digit end
    if value < 10 then num_x = num_x + w_digit end
    
    -- Draw Glow/Blur (Fade out effect)
    if is_low then
        -- Always draw glow when low (pulsing)
        surface.SetFont("AIChaos_HudNumbersGlow")
        surface.SetTextColor(glow_color)
        surface.SetTextPos(num_x, num_y)
        surface.DrawText(val_str)
    else
        -- Normal update flash
        local time_since_change = CurTime() - (last_change_time or 0)
        local blur_alpha = 0
        
        -- Sequence: 0 -> 255 (0.1s Linear), 255 -> 0 (2.0s Deaccel)
        if time_since_change < 0.1 then
            local t = time_since_change / 0.1
            blur_alpha = 255 * InterpLinear(t)
        elseif time_since_change < 2.1 then
            local t = (time_since_change - 0.1) / 2.0
            blur_alpha = 255 * (1 - InterpDeaccel(t))
        end
        
        if blur_alpha > 0 then
            local cur_glow = Color(glow_color.r, glow_color.g, glow_color.b, blur_alpha)
            surface.SetFont("AIChaos_HudNumbersGlow")
            surface.SetTextColor(cur_glow)
            surface.SetTextPos(num_x, num_y)
            surface.DrawText(val_str)
        end
    end
    
    -- Draw Main Number
    surface.SetFont("AIChaos_HudNumbers")
    surface.SetTextColor(text_color)
    surface.SetTextPos(num_x, num_y)
    surface.DrawText(val_str)
    
    return width
end

local function DrawNativeAuxPower(x, y, value, active_items, height, label)
    local scale = GetHudScale()
    
    -- Dimensions from HudLayout.res
    local width = 102 * scale
    
    -- Colors
    local color_high = Color(255, 220, 0, 220)
    local color_low = Color(255, 0, 0, 220)
    
    local color_disabled_high = Color(255, 220, 0, 70)
    local color_disabled_low = Color(255, 0, 0, 70)
    
    local bg_color = Color(0, 0, 0, 76)
    
    local current_color = color_high
    local current_disabled = color_disabled_high
    
    -- Logic if low (< 25)
    -- Reference: SuitAuxPowerDecreasedBelow25 just animates AuxPowerColor to red.
    -- No pulsing, just static color change.
    if value < 25 then
        current_color = color_low
        current_disabled = color_disabled_low
    end
    
    -- Draw Background
    draw.RoundedBox(6, x, y, width, height, bg_color)
    
    -- Bar Properties
    local bar_inset_x = 8 * scale
    local bar_inset_y = 15 * scale
    local bar_width = 92 * scale
    local bar_height = 4 * scale
    local chunk_width = 6 * scale
    local chunk_gap = 3 * scale
    
    -- Calculate Chunks
    -- In C++: int chunkCount = m_flBarWidth / (m_flBarChunkWidth + m_flBarChunkGap);
    local chunk_total_width = chunk_width + chunk_gap
    local chunk_count = math.floor(bar_width / chunk_total_width)
    local enabled_chunks = math.floor(chunk_count * (value / 100) + 0.5)
    
    local cur_x = x + bar_inset_x
    local cur_y = y + bar_inset_y
    
    -- Draw Enabled Chunks
    surface.SetDrawColor(current_color)
    for i = 1, enabled_chunks do
        surface.DrawRect(cur_x, cur_y, chunk_width, bar_height)
        cur_x = cur_x + chunk_total_width
    end
    
    -- Draw Disabled Chunks
    surface.SetDrawColor(current_disabled)
    for i = enabled_chunks + 1, chunk_count do
        surface.DrawRect(cur_x, cur_y, chunk_width, bar_height)
        cur_x = cur_x + chunk_total_width
    end
    
    -- Draw Label
    local text_x = x + (8 * scale)
    local text_y = y + (4 * scale)
    
    surface.SetFont("AIChaos_HudText")
    surface.SetTextColor(current_color)
    surface.SetTextPos(text_x, text_y)
    surface.DrawText(label or "AUX POWER")
    
    -- Draw Active Items (Flashlight, etc)
    local text2_y_start = 22 * scale
    local text2_gap = 10 * scale
    local text2_y = y + text2_y_start
    
    for _, item in ipairs(active_items) do
        surface.SetTextPos(text_x, text2_y)
        surface.DrawText(item)
        text2_y = text2_y + text2_gap
    end
    
    return height
end

local aux_states = {} -- Store height for each aux element
local native_aux_state = { current = 0, target = 0, speed = 0 } -- Store height of native aux power for stacking

-- Test state for randomization
local test_hud_state = { value = 100, last_change = 0, aux_value = 100 }
local next_random_time = 0

hook.Add("HUDPaint", "AIChaos_NativeHudTest", function()
    local ply = LocalPlayer()
    if not IsValid(ply) or not ply:Alive() then return end
    
    -- Randomize Value
    if CurTime() > next_random_time then
        local new_val = math.random(0, 30)
        if new_val ~= test_hud_state.value then
            test_hud_state.value = new_val
            test_hud_state.last_change = CurTime()
        end
        
        -- Randomize Aux Value too
        test_hud_state.aux_value = math.random(0, 40) -- 0 to 40 to trigger low (<25) often
        
        next_random_time = CurTime() + 4
    end
    
    local scale = GetHudScale()
    
    -- Calculate Position
    -- From HudLayout.res: ypos 432 (relative to 480 height)
    -- 480 - 432 = 48 pixels from bottom
    local start_y = ScrH() - (48 * scale)
    
    -- From HudLayout.res:
    -- HudHealth xpos 16, wide 102
    -- HudSuit xpos 140, wide 108
    -- Gap between Health and Suit = 140 - (16 + 102) = 22 pixels
    
    local start_x = 16 * scale
    local health_width = 102 * scale
    local suit_width = 108 * scale
    local gap = 22 * scale
    
    -- Start after Health
    local current_x = start_x + health_width + gap
    
    -- If suit is equipped/visible, shift over past the Suit element
    if ply:Armor() > 0 then
        current_x = current_x + suit_width + gap
    end
    
    -- Draw our new element
    local options = {
        warn_low = true,
        low_threshold = 20
    }
    DrawNativeHudElement(current_x, start_y, "CHAOS", test_hud_state.value, test_hud_state.last_change, options)
    
    -- Simulate some active items
    local active_items = {}
    if ply:FlashlightIsOn() then table.insert(active_items, "FLASHLIGHT") end
    if ply:IsSprinting() then table.insert(active_items, "SPRINT") end
    
    -- Define Aux Elements to stack
    local aux_elements = {
        { id = "chaos_power", label = "CHAOS POWER", value = test_hud_state.aux_value, items = active_items },
        { id = "chaos_power2", label = "SPUNK METER", value = 80, items = active_items }
    }
    
    -- Calculate Native Aux Power State to stack correctly
    local native_active_items = {}
    if ply:WaterLevel() == 3 then table.insert(native_active_items, "OXYGEN") end
    if ply:FlashlightIsOn() then table.insert(native_active_items, "FLASHLIGHT") end
    if ply:IsSprinting() and ply:GetVelocity():Length2D() > 1 then table.insert(native_active_items, "SPRINT") end
    
    local native_target_height = 0
    if ply:IsSuitEquipped() and (ply:GetSuitPower() < 100 or #native_active_items > 0) then
        local base = 26 * scale
        native_target_height = base
        if #native_active_items > 0 then
             local last_item_y = 20 * scale + (#native_active_items - 1) * (10 * scale)
             local text_bottom = last_item_y + (12 * scale)
             local padding = 4 * scale
             native_target_height = math.max(base, text_bottom + padding)
        end
        
        -- Add extra gap when visible
        native_target_height = native_target_height + (6 * scale)
    end
    
    -- Animate Native Height Tracking
    -- Use fixed duration logic (0.4s)
    if native_target_height ~= native_aux_state.target then
        native_aux_state.target = native_target_height
        local diff = math.abs(native_aux_state.target - native_aux_state.current)
        native_aux_state.speed = diff / 0.4 -- 0.4s duration
    end
    
    native_aux_state.current = math.Approach(native_aux_state.current, native_aux_state.target, native_aux_state.speed * FrameTime())
    
    -- Stack them upwards from the main element (Health)
    -- We sit above the native aux element (if visible) or above health (if not)
    -- Gap is 10px.
    local bottom_y = start_y - (6 * scale) - native_aux_state.current

    local aux_base_height = 26 * scale
    
    for _, ele in ipairs(aux_elements) do
        -- Initialize state if needed
        if not aux_states[ele.id] then 
            aux_states[ele.id] = { current = aux_base_height, target = aux_base_height, speed = 0 }
        end
        
        -- Calculate Target Height
        local target_height = aux_base_height
        if #ele.items > 0 then
            local last_item_y = 20 * scale + (#ele.items - 1) * (10 * scale)
            local text_bottom = last_item_y + (12 * scale)
            local padding = 4 * scale
            target_height = math.max(aux_base_height, text_bottom + padding)
        end
        
        -- Animate
        if target_height ~= aux_states[ele.id].target then
            aux_states[ele.id].target = target_height
            local diff = math.abs(aux_states[ele.id].target - aux_states[ele.id].current)
            aux_states[ele.id].speed = diff / 0.4 -- 0.4s duration
        end
        
        aux_states[ele.id].current = math.Approach(aux_states[ele.id].current, aux_states[ele.id].target, aux_states[ele.id].speed * FrameTime())
        
        -- Draw
        local h = aux_states[ele.id].current
        local y = bottom_y - h
        DrawNativeAuxPower(start_x, y, ele.value, ele.items, h, ele.label)
        
        -- Move cursor up for next element
        bottom_y = y - (6 * scale)
    end
end)
