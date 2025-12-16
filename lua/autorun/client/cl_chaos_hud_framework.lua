if SERVER then return end

ChaosHUD = ChaosHUD or {}
ChaosHUD.Version = "1.0"

-- ============================================================================
--  CONSTANTS & STYLING
-- ============================================================================

ChaosHUD.Colors = {
    Yellow      = Color(255, 220, 0, 255),
    Red         = Color(255, 0, 0, 255),
    GlowYellow  = Color(255, 220, 0, 128),
    GlowRed     = Color(255, 0, 0, 128),
    BgStandard  = Color(0, 0, 0, 72),
    BgDark      = Color(0, 0, 0, 76),
    DisabledHigh= Color(255, 220, 0, 70),
    DisabledLow = Color(255, 0, 0, 70),
    
    -- Pulse Colors
    PulseTextBright = Color(255, 80, 0, 255),
    PulseBgBright   = Color(100, 0, 0, 80),
}

ChaosHUD.Styles = {
    CornerRadius = 8,
    AuxCornerRadius = 6,
    Gap = 22, -- Gap between columns
    StackGap = 6, -- Gap between vertical items
}

-- ============================================================================
--  HELPER FUNCTIONS
-- ============================================================================

function ChaosHUD.GetScale()
    return ScrH() / 480
end

function ChaosHUD.InterpLinear(t) return t end
function ChaosHUD.InterpAccel(t) return t * t end
function ChaosHUD.InterpDeaccel(t) return 1 - (1 - t) * (1 - t) end

function ChaosHUD.UpdateFonts()
    local scale = ChaosHUD.GetScale()
    
    surface.CreateFont( "ChaosHUD_Numbers", {
        font = "Halflife2",
        size = math.Round(32 * scale),
        weight = 0,
        antialias = true,
        additive = true,
    } )

    surface.CreateFont( "ChaosHUD_NumbersGlow", {
        font = "Halflife2",
        size = math.Round(32 * scale),
        weight = 0,
        blursize  = math.Round(4 * scale),
        scanlines = math.Round(2 * scale),
        antialias = true,
        additive = true,
    } )

    surface.CreateFont( "ChaosHUD_NumbersSmall", {
        font = "Halflife2",
        size = math.Round(16 * scale),
        weight = 1000,
        antialias = true,
        additive = true,
    } )

    surface.CreateFont( "ChaosHUD_Text", {
        font = "Verdana",
        size = math.Round(8 * scale),
        weight = 900,
        antialias = true,
        additive = true,
    } )

    surface.CreateFont( "ChaosHUD_SmallText", {
        font = "Verdana",
        size = math.Round(6 * scale),
        weight = 700,
        antialias = true,
        additive = true,
    } )
end

hook.Add("OnScreenSizeChanged", "ChaosHUD_UpdateFonts", ChaosHUD.UpdateFonts)
ChaosHUD.UpdateFonts()

-- ============================================================================
--  LAYOUT SYSTEM
-- ============================================================================

-- Structure:
-- ChaosHUD.HStack = {
--    { id = "Health", width = 102, visible = true, vstack = { ... } },
--    { id = "Suit", width = 108, visible = func, vstack = { ... } },
-- }

ChaosHUD.HStack = {}
ChaosHUD.HStackMap = {} -- Quick lookup by ID

ChaosHUD.RightHStack = {}
ChaosHUD.RightHStackMap = {}

ChaosHUD.CenterVStack = {}
ChaosHUD.CenterVStackMap = {}

ChaosHUD.TopLeftVStack = {}
ChaosHUD.TopLeftVStackMap = {}

ChaosHUD.TopRightVStack = {}
ChaosHUD.TopRightVStackMap = {}

function ChaosHUD.RegisterColumn(id, width_base, visibility_callback, priority)
    if ChaosHUD.HStackMap[id] then return end -- Already exists
    
    local col = {
        id = id,
        width_base = width_base,
        check_visible = visibility_callback or function() return true end,
        priority = priority or 100,
        vstack = {},
        vstack_map = {}
    }
    
    table.insert(ChaosHUD.HStack, col)
    ChaosHUD.HStackMap[id] = col
    
    -- Sort by priority
    table.sort(ChaosHUD.HStack, function(a, b) return a.priority < b.priority end)
end

function ChaosHUD.RegisterRightColumn(id, width_base, visibility_callback, priority)
    if ChaosHUD.RightHStackMap[id] then return end -- Already exists
    
    local col = {
        id = id,
        width_base = width_base,
        check_visible = visibility_callback or function() return true end,
        priority = priority or 100,
        vstack = {},
        vstack_map = {}
    }
    
    table.insert(ChaosHUD.RightHStack, col)
    ChaosHUD.RightHStackMap[id] = col
    
    -- Sort by priority (Low priority = Closer to Right Edge)
    table.sort(ChaosHUD.RightHStack, function(a, b) return a.priority < b.priority end)
end

function ChaosHUD.AddVStackElement(column_id, element_id, element_obj, priority)
    local col = ChaosHUD.HStackMap[column_id]
    if not col then return end
    
    if col.vstack_map[element_id] then
        -- Update existing?
        for k, v in ipairs(col.vstack) do
            if v.id == element_id then
                col.vstack[k].obj = element_obj
                col.vstack[k].priority = priority or v.priority
                break
            end
        end
    else
        local item = {
            id = element_id,
            obj = element_obj,
            priority = priority or 100,
            state = { current_h = 0, target_h = 0, speed = 0 } -- Animation state
        }
        table.insert(col.vstack, item)
        col.vstack_map[element_id] = item
    end
    
    table.sort(col.vstack, function(a, b) return a.priority < b.priority end)
end

function ChaosHUD.AddRightVStackElement(column_id, element_id, element_obj, priority)
    local col = ChaosHUD.RightHStackMap[column_id]
    if not col then return end
    
    if col.vstack_map[element_id] then
        -- Update existing?
        for k, v in ipairs(col.vstack) do
            if v.id == element_id then
                col.vstack[k].obj = element_obj
                col.vstack[k].priority = priority or v.priority
                break
            end
        end
    else
        local item = {
            id = element_id,
            obj = element_obj,
            priority = priority or 100,
            state = { current_h = 0, target_h = 0, speed = 0 } -- Animation state
        }
        table.insert(col.vstack, item)
        col.vstack_map[element_id] = item
    end
    
    table.sort(col.vstack, function(a, b) return a.priority < b.priority end)
end

function ChaosHUD.RemoveVStackElement(column_id, element_id)
    local col = ChaosHUD.HStackMap[column_id] or ChaosHUD.RightHStackMap[column_id]
    if not col then return end
    
    if col.vstack_map[element_id] then
        col.vstack_map[element_id] = nil
        for k, v in ipairs(col.vstack) do
            if v.id == element_id then
                table.remove(col.vstack, k)
                break
            end
        end
    end
end

function ChaosHUD.AddCenterElement(element_id, element_obj, priority)
    if ChaosHUD.CenterVStackMap[element_id] then
        -- Update existing
        for k, v in ipairs(ChaosHUD.CenterVStack) do
            if v.id == element_id then
                v.obj = element_obj
                v.priority = priority or v.priority
                break
            end
        end
    else
        local item = {
            id = element_id,
            obj = element_obj,
            priority = priority or 100,
            state = { current_h = 0, target_h = 0, speed = 0 }
        }
        table.insert(ChaosHUD.CenterVStack, item)
        ChaosHUD.CenterVStackMap[element_id] = item
    end
    table.sort(ChaosHUD.CenterVStack, function(a, b) return a.priority < b.priority end)
end

function ChaosHUD.RemoveCenterElement(element_id)
    if ChaosHUD.CenterVStackMap[element_id] then
        ChaosHUD.CenterVStackMap[element_id] = nil
        for k, v in ipairs(ChaosHUD.CenterVStack) do
            if v.id == element_id then
                table.remove(ChaosHUD.CenterVStack, k)
                break
            end
        end
    end
end

function ChaosHUD.AddTopLeftElement(element_id, element_obj, priority)
    if ChaosHUD.TopLeftVStackMap[element_id] then
        for k, v in ipairs(ChaosHUD.TopLeftVStack) do
            if v.id == element_id then
                v.obj = element_obj
                v.priority = priority or v.priority
                break
            end
        end
    else
        local item = {
            id = element_id,
            obj = element_obj,
            priority = priority or 100,
            state = { current_h = 0, target_h = 0, speed = 0 }
        }
        table.insert(ChaosHUD.TopLeftVStack, item)
        ChaosHUD.TopLeftVStackMap[element_id] = item
    end
    table.sort(ChaosHUD.TopLeftVStack, function(a, b) return a.priority < b.priority end)
end

function ChaosHUD.RemoveTopLeftElement(element_id)
    if ChaosHUD.TopLeftVStackMap[element_id] then
        ChaosHUD.TopLeftVStackMap[element_id] = nil
        for k, v in ipairs(ChaosHUD.TopLeftVStack) do
            if v.id == element_id then
                table.remove(ChaosHUD.TopLeftVStack, k)
                break
            end
        end
    end
end

function ChaosHUD.AddTopRightElement(element_id, element_obj, priority)
    if ChaosHUD.TopRightVStackMap[element_id] then
        for k, v in ipairs(ChaosHUD.TopRightVStack) do
            if v.id == element_id then
                v.obj = element_obj
                v.priority = priority or v.priority
                break
            end
        end
    else
        local item = {
            id = element_id,
            obj = element_obj,
            priority = priority or 100,
            state = { current_h = 0, target_h = 0, speed = 0 }
        }
        table.insert(ChaosHUD.TopRightVStack, item)
        ChaosHUD.TopRightVStackMap[element_id] = item
    end
    table.sort(ChaosHUD.TopRightVStack, function(a, b) return a.priority < b.priority end)
end

function ChaosHUD.RemoveTopRightElement(element_id)
    if ChaosHUD.TopRightVStackMap[element_id] then
        ChaosHUD.TopRightVStackMap[element_id] = nil
        for k, v in ipairs(ChaosHUD.TopRightVStack) do
            if v.id == element_id then
                table.remove(ChaosHUD.TopRightVStack, k)
                break
            end
        end
    end
end

-- ============================================================================
--  DRAWING PRIMITIVES
-- ============================================================================

-- Draws a standard HL2 numeric display (like Health/Suit)
-- Returns: width, height
function ChaosHUD.DrawNumericDisplay(x, y, label, value, last_change_time, options)
    options = options or {}
    local warn_low = options.warn_low or false
    local low_threshold = options.low_threshold or 20
    local scale = ChaosHUD.GetScale()
    
    local width = 102 * scale
    local height = 36 * scale
    
    -- Colors
    local is_low = warn_low and (value <= low_threshold)
    local text_color = ChaosHUD.Colors.Yellow
    local glow_color = ChaosHUD.Colors.GlowYellow
    local bg_color = ChaosHUD.Colors.BgStandard
    
    if is_low then
        text_color = ChaosHUD.Colors.Red
        
        -- Pulse Logic
        local cycle = math.fmod(CurTime(), 0.8)
        local t = 0
        
        if cycle < 0.1 then
            t = ChaosHUD.InterpLinear(cycle / 0.1)
            bg_color = Color(
                Lerp(t, ChaosHUD.Colors.BgStandard.r, ChaosHUD.Colors.PulseBgBright.r),
                Lerp(t, ChaosHUD.Colors.BgStandard.g, ChaosHUD.Colors.PulseBgBright.g),
                Lerp(t, ChaosHUD.Colors.BgStandard.b, ChaosHUD.Colors.PulseBgBright.b),
                Lerp(t, ChaosHUD.Colors.BgStandard.a, ChaosHUD.Colors.PulseBgBright.a)
            )
            local glow_alpha = Lerp(t, 100, 255)
            glow_color = Color(255, 0, 0, glow_alpha)
        else
            t = ChaosHUD.InterpDeaccel((cycle - 0.1) / 0.7)
            bg_color = Color(
                Lerp(t, ChaosHUD.Colors.PulseBgBright.r, ChaosHUD.Colors.BgStandard.r),
                Lerp(t, ChaosHUD.Colors.PulseBgBright.g, ChaosHUD.Colors.BgStandard.g),
                Lerp(t, ChaosHUD.Colors.PulseBgBright.b, ChaosHUD.Colors.BgStandard.b),
                Lerp(t, ChaosHUD.Colors.PulseBgBright.a, ChaosHUD.Colors.BgStandard.a)
            )
            local glow_alpha = Lerp(t, 255, 100)
            glow_color = Color(255, 0, 0, glow_alpha)
        end
    end
    
    -- Draw BG
    draw.RoundedBox(ChaosHUD.Styles.CornerRadius, x, y, width, height, bg_color)
    
    -- Draw Label
    surface.SetFont("ChaosHUD_Text")
    surface.SetTextColor(text_color)
    surface.SetTextPos(x + (8 * scale), y + (20 * scale))
    surface.DrawText(label)
    
    -- Draw Number
    local val_str = tostring(value)
    surface.SetFont("ChaosHUD_Numbers")
    local w_digit = surface.GetTextSize("0")
    local num_x = x + (50 * scale)
    local num_y = y + (2 * scale)
    
    -- Left aligned
    
    -- Glow
    if is_low then
        surface.SetFont("ChaosHUD_NumbersGlow")
        surface.SetTextColor(glow_color)
        surface.SetTextPos(num_x, num_y)
        surface.DrawText(val_str)
    else
        local time_since = CurTime() - (last_change_time or 0)
        local blur_alpha = 0
        if time_since < 0.1 then
            blur_alpha = 255 * ChaosHUD.InterpLinear(time_since / 0.1)
        elseif time_since < 2.1 then
            blur_alpha = 255 * (1 - ChaosHUD.InterpDeaccel((time_since - 0.1) / 2.0))
        end
        
        if blur_alpha > 0 then
            local c = Color(glow_color.r, glow_color.g, glow_color.b, blur_alpha)
            surface.SetFont("ChaosHUD_NumbersGlow")
            surface.SetTextColor(c)
            surface.SetTextPos(num_x, num_y)
            surface.DrawText(val_str)
        end
    end
    
    surface.SetFont("ChaosHUD_Numbers")
    surface.SetTextColor(text_color)
    surface.SetTextPos(num_x, num_y)
    surface.DrawText(val_str)
    
    return width, height
end

-- Draws an Ammo Display (Primary + Secondary)
-- Returns: width, height
function ChaosHUD.DrawAmmoDisplay(x, y, label, value, value2, last_change_time, last_weapon_change_time, options)
    options = options or {}
    local scale = ChaosHUD.GetScale()
    
    local show_secondary = (value2 ~= nil)
    
    local width = (show_secondary and 132 or 102) * scale
    local height = 36 * scale
    
    -- Animation State
    local time_since_weapon = CurTime() - (last_weapon_change_time or 0)
    local pulse_alpha = 0
    
    -- Pulse Logic (Background & Text Brightness)
    -- Native HUD pulses background on weapon switch
    if time_since_weapon < 0.5 then
        -- Fade out over 0.5s
        pulse_alpha = 255 * (1 - (time_since_weapon / 0.5))
    end

    -- Colors
    local text_color = ChaosHUD.Colors.Yellow
    local glow_color = ChaosHUD.Colors.GlowYellow
    local bg_color = ChaosHUD.Colors.BgStandard
    
    -- Red Text if Empty
    if value == 0 then
        text_color = ChaosHUD.Colors.Red
        glow_color = ChaosHUD.Colors.Red -- Glow should also be red? Or maybe keep yellow glow? Native turns red.
    end

    -- Apply Pulse to Background
    if pulse_alpha > 0 then
        -- Lerp from Yellow to Standard BG
        -- Actually native HUD flashes the BG yellow then fades to dark
        local r = Lerp(pulse_alpha / 255, bg_color.r, ChaosHUD.Colors.Yellow.r)
        local g = Lerp(pulse_alpha / 255, bg_color.g, ChaosHUD.Colors.Yellow.g)
        local b = Lerp(pulse_alpha / 255, bg_color.b, ChaosHUD.Colors.Yellow.b)
        local a = Lerp(pulse_alpha / 255, bg_color.a, 100) -- Pulse is slightly more opaque
        bg_color = Color(r, g, b, a)
        
        -- Pulse text to be brighter/white? Native just pulses BG mostly.
        -- But let's make text slightly brighter if needed.
    end
    
    -- Draw BG
    draw.RoundedBox(ChaosHUD.Styles.CornerRadius, x, y, width, height, bg_color)
    
    -- Draw Label
    surface.SetFont("ChaosHUD_Text")
    surface.SetTextColor(text_color)
    surface.SetTextPos(x + (8 * scale), y + (20 * scale))
    surface.DrawText(label)
    
    -- Draw Primary Number
    local val_str = tostring(value)
    surface.SetFont("ChaosHUD_Numbers")
    local w_digit = surface.GetTextSize("0")
    local num_x = x + (44 * scale)
    local num_y = y + (2 * scale)
    
    -- Left aligned
    
    -- Glow Logic (Primary)
    local time_since = CurTime() - (last_change_time or 0)
    local blur_alpha = 0
    if time_since < 0.1 then
        blur_alpha = 255 * ChaosHUD.InterpLinear(time_since / 0.1)
    elseif time_since < 2.1 then
        blur_alpha = 255 * (1 - ChaosHUD.InterpDeaccel((time_since - 0.1) / 2.0))
    end
    
    if blur_alpha > 0 then
        local c = Color(glow_color.r, glow_color.g, glow_color.b, blur_alpha)
        surface.SetFont("ChaosHUD_NumbersGlow")
        surface.SetTextColor(c)
        surface.SetTextPos(num_x, num_y)
        surface.DrawText(val_str)
    end
    
    surface.SetFont("ChaosHUD_Numbers")
    surface.SetTextColor(text_color)
    surface.SetTextPos(num_x, num_y)
    surface.DrawText(val_str)
    
    -- Draw Secondary Number (if applicable)
    if show_secondary then
        local val2_str = tostring(value2)
        local num2_x = x + (98 * scale)
        local num2_y = y + (16 * scale)
        
        surface.SetFont("ChaosHUD_NumbersSmall")
        local w_digit_small = surface.GetTextSize("0")
        
        -- Adjust X for digits (Right Alignment)
        surface.SetTextColor(text_color)
        surface.SetTextPos(num2_x, num2_y)
        surface.DrawText(val2_str)
    end
    
    return width, height
end

-- Draws an Aux Power style bar
function ChaosHUD.DrawAuxBar(x, y, label, value, active_items, height_override, only_calc, color_override)
    local scale = ChaosHUD.GetScale()
    local width = 102 * scale
    
    -- Calculate Height if not provided (for layout prediction)
    local base_height = 26 * scale
    local calc_height = base_height
    if active_items and #active_items > 0 then
        local last_item_y = 20 * scale + (#active_items - 1) * (10 * scale)
        local text_bottom = last_item_y + (12 * scale)
        calc_height = math.max(base_height, text_bottom + (4 * scale))
    end
    
    local height = height_override or calc_height
    
    if only_calc then return width, calc_height end

    -- Colors
    local col_active = ChaosHUD.Colors.Yellow
    local col_disabled = ChaosHUD.Colors.DisabledHigh
    local bg_color = ChaosHUD.Colors.BgDark
    
    if color_override then
        col_active = color_override
        col_disabled = Color(col_active.r, col_active.g, col_active.b, 70)
    elseif value < 25 then
        col_active = ChaosHUD.Colors.Red
        col_disabled = ChaosHUD.Colors.DisabledLow
    end
    
    -- Draw BG
    draw.RoundedBox(ChaosHUD.Styles.AuxCornerRadius, x, y, width, height, bg_color)
    
    -- Draw Bar
    local bar_inset_x = 8 * scale
    local bar_inset_y = 15 * scale
    local bar_width = 92 * scale
    local bar_height = 4 * scale
    local chunk_width = 6 * scale
    local chunk_gap = 3 * scale
    
    local chunk_total = chunk_width + chunk_gap
    local chunk_count = math.floor(bar_width / chunk_total)
    local enabled_chunks = math.floor(chunk_count * (value / 100) + 0.5)
    
    local cur_x = x + bar_inset_x
    local cur_y = y + bar_inset_y
    
    surface.SetDrawColor(col_active)
    for i = 1, enabled_chunks do
        surface.DrawRect(cur_x, cur_y, chunk_width, bar_height)
        cur_x = cur_x + chunk_total
    end
    
    surface.SetDrawColor(col_disabled)
    for i = enabled_chunks + 1, chunk_count do
        surface.DrawRect(cur_x, cur_y, chunk_width, bar_height)
        cur_x = cur_x + chunk_total
    end
    
    -- Label
    surface.SetFont("ChaosHUD_Text")
    surface.SetTextColor(col_active)
    surface.SetTextPos(x + (8 * scale), y + (4 * scale))
    surface.DrawText(label or "AUX")
    
    -- Items
    if active_items then
        local item_y = y + (22 * scale)
        for _, item in ipairs(active_items) do
            surface.SetTextPos(x + (8 * scale), item_y)
            surface.DrawText(item)
            item_y = item_y + (10 * scale)
        end
    end
    
    return width, calc_height
end

-- ============================================================================
--  MAIN RENDER LOOP
-- ============================================================================

-- Register Default Columns
ChaosHUD.RegisterColumn("Health", 102, nil, 10)
ChaosHUD.RegisterColumn("Suit", 108, function() 
    local ply = LocalPlayer()
    return IsValid(ply) and ply:Armor() > 0 
end, 20)

-- Register Default Right Columns
ChaosHUD.RegisterRightColumn("Ammo", 102, nil, 10)

function ChaosHUD.GetNativeAmmoLayout()
    local ply = LocalPlayer()
    if not IsValid(ply) or not ply:Alive() then return 0, 0 end
    
    local wpn = ply:GetActiveWeapon()
    if not IsValid(wpn) then return 0, 0 end
    
    -- Check if weapon uses ammo
    if wpn:GetPrimaryAmmoType() == -1 then return 0, 0 end
    
    -- Logic from C++
    local uses_clips = (wpn:GetMaxClip1() ~= -1) -- Approximation
    local uses_secondary = (wpn:GetSecondaryAmmoType() ~= -1)
    
    -- Calculate "Right" offset (how far from right edge the LEFTMOST pixel is)
    local right_offset = 0
    
    if uses_secondary then
        right_offset = 222 -- r222
    elseif uses_clips then
        right_offset = 150 -- r150
    else
        right_offset = 118 -- r118
    end
    
    return right_offset, 36 -- Width, Height
end

hook.Add("HUDPaint", "ChaosHUD_Render", function()
    local ply = LocalPlayer()
    if not IsValid(ply) or not ply:Alive() then return end
    if ply:InVehicle() and ply:GetVehicle():GetClass() == "prop_vehicle_jeep" then return end -- Hide in jeep usually?
    
    local scale = ChaosHUD.GetScale()
    local start_x = 16 * scale
    local start_y = ScrH() - (48 * scale)
    local gap = ChaosHUD.Styles.Gap * scale
    
    -- ------------------------------------------------------------------------
    -- LEFT STACK RENDER
    -- ------------------------------------------------------------------------
    local current_x = start_x
    
    for _, col in ipairs(ChaosHUD.HStack) do
        if col.check_visible() then
            local col_width = col.width_base * scale
            
            -- Draw Base Element?
            if col.base_element then
                local draw_y = start_y
                
                -- Bottom Align if GetSize is available
                if col.base_element.GetSize then
                    local _, h = col.base_element:GetSize()
                    local standard_h = 36 * scale
                    draw_y = start_y + (standard_h - h)
                end
                
                local w, h = col.base_element:Draw(current_x, draw_y)
                col_width = w -- Update width if dynamic
            end
            
            -- Draw VStack
            local base_height = 36 * scale
            local stack_start_y
            
            -- Calculate actual base height for stacking
            if col.base_element and col.base_element.GetSize then
                local _, h = col.base_element:GetSize()
                -- If base element is taller than standard, we need to start stacking from higher up
                -- But wait, start_y is the TOP of the base element row? 
                -- No, start_y is the TOP of the standard 36px row.
                -- If an element is 50px tall, it draws from start_y + (36 - 50) = start_y - 14.
                -- So the top of the element is at start_y - 14.
                -- The stack should start above that.
                
                -- Let's find the visual top of the base element
                local visual_top = start_y + (36 * scale - h)
                base_height = h -- Not used directly but good to know
                
                -- Stack starts above the visual top
                stack_start_y = visual_top - (ChaosHUD.Styles.StackGap * scale)
            else
                -- Standard behavior
                stack_start_y = start_y - (ChaosHUD.Styles.StackGap * scale)
            end
            
            -- Special Case: Native Aux Power on Health
            if col.id == "Health" then
                -- Calculate native aux power height
                local native_active = {}
                if ply:WaterLevel() == 3 then table.insert(native_active, "OXYGEN") end
                if ply:FlashlightIsOn() then table.insert(native_active, "FLASHLIGHT") end
                if ply:IsSprinting() and ply:GetVelocity():Length2D() > 1 then table.insert(native_active, "SPRINT") end
                
                local native_h = 0
                if ply:GetSuitPower() < 100 or #native_active > 0 then
                    local _, h = ChaosHUD.DrawAuxBar(0, 0, "", 0, native_active, nil, true) -- Just to calc height
                    native_h = h
                end
                
                -- We need to animate this "gap" because the native HUD animates it
                if not col.native_aux_state then col.native_aux_state = { current = 0, target = 0, speed = 0 } end
                
                local target = native_h > 0 and (native_h + (ChaosHUD.Styles.StackGap * scale)) or 0
                
                if target ~= col.native_aux_state.target then
                    col.native_aux_state.target = target
                    col.native_aux_state.speed = math.abs(target - col.native_aux_state.current) / 0.4
                end
                
                col.native_aux_state.current = math.Approach(
                    col.native_aux_state.current, 
                    col.native_aux_state.target, 
                    col.native_aux_state.speed * FrameTime()
                )
                
                stack_start_y = stack_start_y - col.native_aux_state.current
            end
            
            -- Render VStack Items
            local current_y = stack_start_y
            
            for _, item in ipairs(col.vstack) do
                -- Calculate Target Height
                local w, target_h = item.obj:GetSize()
                
                -- Animate Height
                if target_h ~= item.state.target_h then
                    item.state.target_h = target_h
                    item.state.speed = math.abs(target_h - item.state.current_h) / 0.4
                end
                
                item.state.current_h = math.Approach(
                    item.state.current_h,
                    item.state.target_h,
                    item.state.speed * FrameTime()
                )
                
                -- Draw
                if item.state.current_h > 0 then
                    local draw_y = current_y - item.state.current_h
                    item.obj:Draw(current_x, draw_y, item.state.current_h)
                    current_y = draw_y - (ChaosHUD.Styles.StackGap * scale)
                end
            end
            
            current_x = current_x + col_width + gap
        end
    end

    -- ------------------------------------------------------------------------
    -- RIGHT STACK RENDER
    -- ------------------------------------------------------------------------
    local current_x_right = ScrW() - (16 * scale)
    
    for _, col in ipairs(ChaosHUD.RightHStack) do
        if col.check_visible() then
            local col_width = col.width_base * scale
            local base_height = 36 * scale
            
            -- Special Case: Native Ammo
            if col.id == "Ammo" then
                local native_w, native_h = ChaosHUD.GetNativeAmmoLayout()
                local target_w = native_w * scale
                
                -- Animate Width
                if not col.width_state then col.width_state = { current = 0, target = 0, speed = 0 } end
                
                if target_w ~= col.width_state.target then
                    col.width_state.target = target_w
                    col.width_state.speed = math.abs(target_w - col.width_state.current) / 0.4
                end
                
                col.width_state.current = math.Approach(
                    col.width_state.current,
                    col.width_state.target,
                    col.width_state.speed * FrameTime()
                )
                
                col_width = col.width_state.current
                
                -- If width is 0 (hidden), we might want to skip rendering vstack?
                -- But maybe we want to keep vstack visible even if ammo is hidden?
                -- Usually if ammo is hidden, we don't want a gap.
                if col_width <= 1 then 
                    -- Skip this column effectively
                    goto continue_right
                end
            else
                -- Draw Base Element for custom right columns
                if col.base_element then
                    local draw_y = start_y
                    
                    -- We need to know width to position X.
                    -- Let's assume standard width or ask GetSize.
                    if col.base_element.GetSize then
                        local w, h = col.base_element:GetSize()
                        col_width = w
                        
                        -- Bottom Align
                        local standard_h = 36 * scale
                        draw_y = start_y + (standard_h - h)
                    end
                    
                    local draw_x = current_x_right - col_width
                    local w, h = col.base_element:Draw(draw_x, draw_y)
                    col_width = w
                end
            end
            
            -- Render VStack
            local stack_start_y = start_y - (ChaosHUD.Styles.StackGap * scale)
            
            -- Adjust stack start for tall base elements (Right Side)
            if col.base_element and col.base_element.GetSize then
                local _, h = col.base_element:GetSize()
                local visual_top = start_y + (36 * scale - h)
                stack_start_y = visual_top - (ChaosHUD.Styles.StackGap * scale)
            end
            
            local current_y = stack_start_y
            
            -- VStack is aligned to the RIGHT of the column space?
            -- Or Left? Usually Right aligned for Right HUD.
            -- Let's align to the right edge of the column: current_x_right
            -- But items draw from Top-Left usually.
            -- So we need to calculate item width.
            
            for _, item in ipairs(col.vstack) do
                local w, target_h = item.obj:GetSize()
                
                -- Animate Height
                if target_h ~= item.state.target_h then
                    item.state.target_h = target_h
                    item.state.speed = math.abs(target_h - item.state.current_h) / 0.4
                end
                
                item.state.current_h = math.Approach(
                    item.state.current_h,
                    item.state.target_h,
                    item.state.speed * FrameTime()
                )
                
                if item.state.current_h > 0 then
                    local draw_y = current_y - item.state.current_h
                    -- Align Right: x = current_x_right - w
                    -- But wait, if the column is wider than the item (e.g. Ammo is 222 wide, item is 102 wide)
                    -- Should it align to the inner edge or outer edge?
                    -- HL2 HUD usually aligns to the outer edge (screen edge).
                    -- So align to current_x_right.
                    
                    local draw_x = current_x_right - w
                    
                    -- Special case: If it's the Ammo column, we might want to align to the primary ammo display?
                    -- Primary ammo is at r150 (width 132).
                    -- If we align to r0 (screen edge), we are far right.
                    -- The native ammo is right-aligned.
                    -- So aligning to current_x_right is correct.
                    
                    item.obj:Draw(draw_x, draw_y, item.state.current_h)
                    current_y = draw_y - (ChaosHUD.Styles.StackGap * scale)
                end
            end
            
            current_x_right = current_x_right - col_width - gap
            
            ::continue_right::
        end
    end

    -- ------------------------------------------------------------------------
    -- CENTER STACK RENDER
    -- ------------------------------------------------------------------------
    local center_x = ScrW() / 2
    local center_y = start_y -- Same baseline as others
    
    for _, item in ipairs(ChaosHUD.CenterVStack) do
        -- Calculate Target Height
        local w, target_h = item.obj:GetSize()
        
        -- Animate Height
        if target_h ~= item.state.target_h then
            item.state.target_h = target_h
            item.state.speed = math.abs(target_h - item.state.current_h) / 0.4
        end
        
        item.state.current_h = math.Approach(
            item.state.current_h,
            item.state.target_h,
            item.state.speed * FrameTime()
        )
        
        -- Draw
        if item.state.current_h > 0 then
            local draw_y = center_y - item.state.current_h
            -- Center align: x - w/2
            item.obj:Draw(center_x - (w / 2), draw_y, item.state.current_h)
            center_y = draw_y - (ChaosHUD.Styles.StackGap * scale)
        end
    end

    -- ------------------------------------------------------------------------
    -- TOP LEFT STACK RENDER
    -- ------------------------------------------------------------------------
    local tl_x = 16 * scale
    local tl_y = 16 * scale
    
    for _, item in ipairs(ChaosHUD.TopLeftVStack) do
        local w, target_h = item.obj:GetSize()
        
        if target_h ~= item.state.target_h then
            item.state.target_h = target_h
            item.state.speed = math.abs(target_h - item.state.current_h) / 0.4
        end
        
        item.state.current_h = math.Approach(
            item.state.current_h,
            item.state.target_h,
            item.state.speed * FrameTime()
        )
        
        if item.state.current_h > 0 then
            item.obj:Draw(tl_x, tl_y, item.state.current_h)
            tl_y = tl_y + item.state.current_h + (ChaosHUD.Styles.StackGap * scale)
        end
    end

    -- ------------------------------------------------------------------------
    -- TOP RIGHT STACK RENDER
    -- ------------------------------------------------------------------------
    local tr_right_edge = ScrW() - (16 * scale)
    local tr_y = 16 * scale
    
    for _, item in ipairs(ChaosHUD.TopRightVStack) do
        local w, target_h = item.obj:GetSize()
        
        if target_h ~= item.state.target_h then
            item.state.target_h = target_h
            item.state.speed = math.abs(target_h - item.state.current_h) / 0.4
        end
        
        item.state.current_h = math.Approach(
            item.state.current_h,
            item.state.target_h,
            item.state.speed * FrameTime()
        )
        
        if item.state.current_h > 0 then
            local draw_x = tr_right_edge - w
            item.obj:Draw(draw_x, tr_y, item.state.current_h)
            tr_y = tr_y + item.state.current_h + (ChaosHUD.Styles.StackGap * scale)
        end
    end
end)

-- ============================================================================
--  COMPONENT FACTORIES
-- ============================================================================

function ChaosHUD.CreateNumericElement(label, value_func, options)
    local obj = {}
    obj.last_val = 0
    obj.last_change = 0
    
    function obj:GetSize()
        local scale = ChaosHUD.GetScale()
        return 102 * scale, 36 * scale
    end
    
    function obj:Draw(x, y, h)
        local val = value_func()
        if val ~= self.last_val then
            self.last_change = CurTime()
            self.last_val = val
        end
        
        return ChaosHUD.DrawNumericDisplay(x, y, label, val, self.last_change, options)
    end
    
    return obj
end

function ChaosHUD.CreateAuxElement(label, value_func, items_func)
    local obj = {}
    -- Initialize color state
    obj.cur_color = Color(ChaosHUD.Colors.Yellow.r, ChaosHUD.Colors.Yellow.g, ChaosHUD.Colors.Yellow.b, ChaosHUD.Colors.Yellow.a)
    
    function obj:GetSize()
        local val = value_func()
        local items = items_func and items_func() or {}
        local _, h = ChaosHUD.DrawAuxBar(0, 0, label, val, items, nil, true)
        return 102 * ChaosHUD.GetScale(), h
    end
    
    function obj:Draw(x, y, h)
        local val = value_func()
        local items = items_func and items_func() or {}
        
        -- Determine Target Color
        local target = ChaosHUD.Colors.Yellow
        if val < 25 then target = ChaosHUD.Colors.Red end
        
        -- Animate Color (Linear 0.4s)
        local speed = 255 / 0.4
        local dt = FrameTime()
        
        self.cur_color.r = math.Approach(self.cur_color.r, target.r, speed * dt)
        self.cur_color.g = math.Approach(self.cur_color.g, target.g, speed * dt)
        self.cur_color.b = math.Approach(self.cur_color.b, target.b, speed * dt)
        self.cur_color.a = math.Approach(self.cur_color.a, target.a, speed * dt)
        
        return ChaosHUD.DrawAuxBar(x, y, label, val, items, h, false, self.cur_color)
    end
    
    return obj
end

function ChaosHUD.CreateAmmoElement(label, value_func, value2_func, options)
    local obj = {}
    obj.last_val = 0
    obj.last_change = 0
    obj.last_weapon = nil
    obj.last_weapon_change = 0
    
    function obj:GetSize()
        local scale = ChaosHUD.GetScale()
        local val2 = value2_func and value2_func() or nil
        local width = (val2 ~= nil and 132 or 102) * scale
        return width, 36 * scale
    end
    
    function obj:Draw(x, y, h)
        local val = value_func()
        local val2 = value2_func and value2_func() or nil
        
        if val ~= self.last_val then
            self.last_change = CurTime()
            self.last_val = val
        end
        
        -- Check for weapon change
        local ply = LocalPlayer()
        if IsValid(ply) then
            local wpn = ply:GetActiveWeapon()
            if wpn ~= self.last_weapon then
                self.last_weapon_change = CurTime()
                self.last_weapon = wpn
            end
        end
        
        return ChaosHUD.DrawAmmoDisplay(x, y, label, val, val2, self.last_change, self.last_weapon_change, options)
    end
    
    return obj
end
