if SERVER then return end

-- ============================================================================
--  CHudNumericDisplay - Base class for numeric HUD displays
--  
--  Port of CHudNumericDisplay from Source SDK 2013
--  Used by HudHealth, HudBattery (Suit), HudAmmo, etc.
-- ============================================================================

-- Wait for CHudElement to load
timer.Simple(0.1, function()
    if not CHudElement then
        print("[CHudNumericDisplay] Error: CHudElement not loaded!")
        return
    end

    CHudNumericDisplay = setmetatable({}, { __index = CHudElement })
    CHudNumericDisplay.__index = CHudNumericDisplay

    function CHudNumericDisplay:New(elementName, panelName)
        local obj = CHudElement.New(self, elementName)
        setmetatable(obj, self)
        
        obj.m_iValue = 0
        obj.m_iSecondaryValue = 0
        obj.m_LabelText = ""
        obj.m_bDisplayValue = true
        obj.m_bDisplaySecondaryValue = false
        obj.m_bIndent = false
        obj.m_bIsTime = false
        obj.m_flBlur = 0.0
        
        -- Layout values from HudLayout.res (will be overridden by theme/layout)
        obj.text_xpos = 8
        obj.text_ypos = 20
        obj.digit_xpos = 50
        obj.digit_ypos = 2
        obj.digit2_xpos = 98
        obj.digit2_ypos = 16
        
        -- Font names
        obj.m_hNumberFont = "HudNumbers"
        obj.m_hNumberGlowFont = "HudNumbersGlow"
        obj.m_hSmallNumberFont = "HudNumbersSmall"
        obj.m_hTextFont = "HudSelectionText"
        
        return obj
    end

    function CHudNumericDisplay:Reset()
        self.m_flBlur = 0.0
    end

    function CHudNumericDisplay:SetDisplayValue(value)
        self.m_iValue = value
    end

    function CHudNumericDisplay:SetSecondaryValue(value)
        self.m_iSecondaryValue = value
    end

    function CHudNumericDisplay:SetShouldDisplayValue(state)
        self.m_bDisplayValue = state
    end

    function CHudNumericDisplay:SetShouldDisplaySecondaryValue(state)
        self.m_bDisplaySecondaryValue = state
    end

    function CHudNumericDisplay:SetLabelText(text)
        self.m_LabelText = text or ""
    end

    function CHudNumericDisplay:SetIndent(state)
        self.m_bIndent = state
    end

    function CHudNumericDisplay:SetIsTime(state)
        self.m_bIsTime = state
    end

    -- Paint a number at the specified position
    function CHudNumericDisplay:PaintNumbers(font, xpos, ypos, value)
        surface.SetFont(font)
        
        local unicode
        if not self.m_bIsTime then
            unicode = tostring(value)
        else
            local iMinutes = math.floor(value / 60)
            local iSeconds = value - iMinutes * 60
            if iSeconds < 10 then
                unicode = string.format("%d`0%d", iMinutes, iSeconds)
            else
                unicode = string.format("%d`%d", iMinutes, iSeconds)
            end
        end
        
        -- Adjust position for indenting (right-align for small numbers)
        local charWidth = surface.GetTextSize("0")
        if value < 100 and self.m_bIndent then
            xpos = xpos + charWidth
        end
        if value < 10 and self.m_bIndent then
            xpos = xpos + charWidth
        end
        
        surface.SetTextPos(xpos, ypos)
        surface.DrawText(unicode)
    end

    -- Draw the label text
    function CHudNumericDisplay:PaintLabel()
        surface.SetFont(self.m_hTextFont)
        surface.SetTextColor(self:GetFgColor())
        surface.SetTextPos(self.text_xpos, self.text_ypos)
        surface.DrawText(self.m_LabelText)
    end

    -- Main paint function - renders the numeric display
    function CHudNumericDisplay:Paint()
        if self.m_bDisplayValue then
            -- Draw main number
            surface.SetTextColor(self:GetFgColor())
            self:PaintNumbers(self.m_hNumberFont, self.digit_xpos, self.digit_ypos, self.m_iValue)
            
            -- Draw overbright blur/glow
            for fl = self.m_flBlur, 0.0, -1.0 do
                if fl >= 1.0 then
                    self:PaintNumbers(self.m_hNumberGlowFont, self.digit_xpos, self.digit_ypos, self.m_iValue)
                elseif fl > 0.0 then
                    -- Draw partial glow
                    local col = self:GetFgColor()
                    col = Color(col.r, col.g, col.b, col.a * fl)
                    surface.SetTextColor(col)
                    self:PaintNumbers(self.m_hNumberGlowFont, self.digit_xpos, self.digit_ypos, self.m_iValue)
                end
            end
        end
        
        -- Draw secondary value (ammo reserve, etc.)
        if self.m_bDisplaySecondaryValue then
            surface.SetTextColor(self:GetFgColor())
            self:PaintNumbers(self.m_hSmallNumberFont, self.digit2_xpos, self.digit2_ypos, self.m_iSecondaryValue)
        end
        
        -- Draw label
        self:PaintLabel()
    end

    -- Get foreground color from theme
    function CHudNumericDisplay:GetFgColor()
        if HudTheme then
            local theme = HudTheme.GetCurrent()
            return theme.Colors.BrightFg
        end
        return Color(255, 220, 0, 255)
    end

    -- Apply layout from HudLayout.res
    function CHudNumericDisplay:ApplyLayout(layout)
        if not layout then return end
        
        self.text_xpos = layout.text_xpos or self.text_xpos
        self.text_ypos = layout.text_ypos or self.text_ypos
        self.digit_xpos = layout.digit_xpos or self.digit_xpos
        self.digit_ypos = layout.digit_ypos or self.digit_ypos
        self.digit2_xpos = layout.digit2_xpos or self.digit2_xpos
        self.digit2_ypos = layout.digit2_ypos or self.digit2_ypos
    end

    print("[CHudNumericDisplay] Loaded - Base class for numeric HUD displays")
end)
