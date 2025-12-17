if SERVER then return end

-- ============================================================================
--  HUD Settings Menu - Theme selection and configuration
-- ============================================================================

local PANEL = {}

function PANEL:Init()
    self:SetTitle("HUD Settings")
    self:SetSize(500, 400)
    self:Center()
    self:MakePopup()
    
    -- Theme selection
    local themeLabel = vgui.Create("DLabel", self)
    themeLabel:SetPos(20, 40)
    themeLabel:SetSize(100, 20)
    themeLabel:SetText("HUD Theme:")
    
    local themeCombo = vgui.Create("DComboBox", self)
    themeCombo:SetPos(120, 40)
    themeCombo:SetSize(200, 20)
    
    -- Add themes
    for name, theme in pairs(HudTheme.Themes) do
        themeCombo:AddChoice(theme.Name, name)
    end
    
    -- Set current theme
    local currentTheme = HudTheme.GetCurrent()
    if currentTheme then
        themeCombo:SetValue(currentTheme.Name)
    end
    
    -- Theme change handler
    themeCombo.OnSelect = function(panel, index, value, data)
        HudTheme.SetTheme(data)
    end
    
    -- Theme preview panel
    local previewPanel = vgui.Create("DPanel", self)
    previewPanel:SetPos(20, 80)
    previewPanel:SetSize(460, 240)
    
    function previewPanel:Paint(w, h)
        local theme = HudTheme.GetCurrent()
        if not theme then return end
        
        -- Draw background
        draw.RoundedBox(4, 0, 0, w, h, Color(30, 30, 30, 255))
        
        -- Draw theme info
        draw.SimpleText("Theme: " .. theme.Name, "DermaDefault", 10, 10, Color(255, 255, 255))
        draw.SimpleText(theme.Description, "DermaDefault", 10, 30, Color(200, 200, 200))
        
        -- Draw color swatches
        draw.SimpleText("Colors:", "DermaDefault", 10, 60, Color(255, 255, 255))
        
        local colorY = 80
        local colorNames = {"BrightFg", "Caution", "BgColor", "AuxPowerHigh", "AuxPowerLow"}
        
        for _, colorName in ipairs(colorNames) do
            local color = theme.Colors[colorName]
            if color then
                draw.RoundedBox(2, 10, colorY, 30, 20, color)
                draw.SimpleText(colorName, "DermaDefault", 50, colorY + 3, Color(255, 255, 255))
                colorY = colorY + 25
            end
        end
        
        -- Draw layout info
        draw.SimpleText("Layout:", "DermaDefault", 240, 60, Color(255, 255, 255))
        draw.SimpleText("Corner Radius: " .. theme.Layout.CornerRadius, "DermaDefault", 240, 80, Color(200, 200, 200))
        draw.SimpleText("Gap: " .. theme.Layout.Gap, "DermaDefault", 240, 100, Color(200, 200, 200))
        draw.SimpleText("Stack Gap: " .. theme.Layout.StackGap, "DermaDefault", 240, 120, Color(200, 200, 200))
    end
    
    -- Advanced settings
    local advLabel = vgui.Create("DLabel", self)
    advLabel:SetPos(20, 330)
    advLabel:SetSize(200, 20)
    advLabel:SetText("Advanced Settings:")
    
    -- Show native HUD elements checkbox
    local showNativeCheck = vgui.Create("DCheckBoxLabel", self)
    showNativeCheck:SetPos(20, 355)
    showNativeCheck:SetText("Enable Native HUD Elements")
    showNativeCheck:SetValue(1)
    showNativeCheck:SizeToContents()
    
    -- Close button
    local closeBtn = vgui.Create("DButton", self)
    closeBtn:SetText("Close")
    closeBtn:SetPos(400, 360)
    closeBtn:SetSize(80, 25)
    closeBtn.DoClick = function()
        self:Close()
    end
    
    -- Apply button
    local applyBtn = vgui.Create("DButton", self)
    applyBtn:SetText("Apply")
    applyBtn:SetPos(310, 360)
    applyBtn:SetSize(80, 25)
    applyBtn.DoClick = function()
        HudTheme.ApplyTheme()
        chat.AddText(Color(100, 255, 100), "[HUD] ", Color(255, 255, 255), "Settings applied!")
    end
end

vgui.Register("ChaosHUDSettings", PANEL, "DFrame")

-- Console command to open settings
concommand.Add("chaos_hud_settings", function()
    vgui.Create("ChaosHUDSettings")
end)

-- Add to F1 menu or similar (if it exists)
hook.Add("OnSpawnMenuOpen", "ChaosHUD_AddSettingsToSpawnMenu", function()
    -- This will be called when spawn menu opens
    -- You can add a button to open settings here if desired
end)

print("[HudSettings] Loaded - Use 'chaos_hud_settings' to open the settings menu")
