if SERVER then return end

-- ============================================================================
--  HUD Resource Parser
--  
--  Parses Source Engine resource files (.res format) for:
--  - HudLayout.res (element positioning and sizing)
--  - ClientScheme.res (colors, fonts, borders)
--  - HudAnimations.txt (animation events)
-- ============================================================================

HudResources = HudResources or {}

-- ============================================================================
--  Resource File Parser (Simple .res format parser)
-- ============================================================================

local function ParseValue(value)
    -- Remove quotes
    value = string.Trim(value)
    if string.sub(value, 1, 1) == '"' and string.sub(value, -1) == '"' then
        value = string.sub(value, 2, -2)
    end
    
    -- Try to parse as number
    local num = tonumber(value)
    if num then return num end
    
    -- Try to parse as color (R G B A format)
    local r, g, b, a = string.match(value, "(%d+)%s+(%d+)%s+(%d+)%s+(%d+)")
    if r then
        return Color(tonumber(r), tonumber(g), tonumber(b), tonumber(a))
    end
    
    -- Return as string
    return value
end

local function ParseBlock(lines, startIdx)
    local block = {}
    local i = startIdx
    local depth = 0
    
    while i <= #lines do
        local line = string.Trim(lines[i])
        
        -- Skip comments and empty lines
        if line ~= "" and not string.StartWith(line, "//") then
            if string.find(line, "{") then
                depth = depth + 1
                if depth == 1 then
                    -- Start of block, get the name
                    local name = string.match(line, "^([^{]+)")
                    if name then
                        block._name = string.Trim(string.gsub(name, '"', ''))
                    end
                end
            elseif string.find(line, "}") then
                depth = depth - 1
                if depth == 0 then
                    return block, i + 1
                end
            elseif depth > 0 then
                -- Parse key-value pair or nested block
                local key, value = string.match(line, '^"?([^"]+)"?%s+"(.+)"')
                if key and value then
                    key = string.Trim(key)
                    block[key] = ParseValue(value)
                elseif string.find(line, "{", 1, true) then
                    -- Nested block
                    local nestedBlock, newIdx = ParseBlock(lines, i)
                    if nestedBlock._name then
                        block[nestedBlock._name] = nestedBlock
                    end
                    i = newIdx - 1
                end
            end
        end
        
        i = i + 1
    end
    
    return block, i
end

function HudResources.ParseResFile(content)
    local lines = string.Explode("\n", content)
    local result = {}
    local i = 1
    
    while i <= #lines do
        local line = string.Trim(lines[i])
        
        if line ~= "" and not string.StartWith(line, "//") then
            if string.find(line, "{") then
                local block, newIdx = ParseBlock(lines, i)
                if block._name then
                    result[block._name] = block
                end
                i = newIdx
            else
                i = i + 1
            end
        else
            i = i + 1
        end
    end
    
    return result
end

-- ============================================================================
--  HudLayout.res Storage
-- ============================================================================

HudResources.Layout = HudResources.Layout or {}

function HudResources.GetElementLayout(elementName)
    return HudResources.Layout[elementName]
end

function HudResources.SetElementLayout(elementName, layout)
    HudResources.Layout[elementName] = layout
end

-- ============================================================================
--  ClientScheme.res Storage
-- ============================================================================

HudResources.Scheme = HudResources.Scheme or {
    Colors = {},
    Fonts = {},
    Borders = {},
    BaseSettings = {}
}

function HudResources.GetColor(name)
    return HudResources.Scheme.Colors[name]
end

function HudResources.GetFont(name)
    return HudResources.Scheme.Fonts[name]
end

function HudResources.GetBorder(name)
    return HudResources.Scheme.Borders[name]
end

function HudResources.GetBaseSetting(name)
    return HudResources.Scheme.BaseSettings[name]
end

-- ============================================================================
--  Helper Functions
-- ============================================================================

-- Convert layout position to screen coordinates
-- Supports: absolute (16), right-aligned (r150), centered (c-100)
function HudResources.ConvertPosition(value, screenDim)
    if type(value) == "string" then
        if string.StartWith(value, "r") then
            -- Right-aligned
            local offset = tonumber(string.sub(value, 2))
            return screenDim - offset
        elseif string.StartWith(value, "c") then
            -- Center-aligned
            local offset = tonumber(string.sub(value, 2)) or 0
            return (screenDim / 2) + offset
        end
    end
    
    return tonumber(value) or 0
end

-- Get scaled dimension (handles "f0" for full screen)
function HudResources.ConvertDimension(value, screenDim)
    if type(value) == "string" then
        if string.StartWith(value, "f") then
            -- Full screen with offset
            local offset = tonumber(string.sub(value, 2)) or 0
            return screenDim + offset
        end
    end
    
    return tonumber(value) or 0
end

-- Create a font from scheme data
function HudResources.CreateFont(name, fontData)
    if not fontData then return end
    
    local fontName = fontData.name or "Arial"
    local fontSize = fontData.tall or 12
    local fontWeight = fontData.weight or 400
    local outline = fontData.outline == 1
    local additive = fontData.additive == 1
    local antialias = fontData.antialias ~= 0
    local blur = fontData.blur or 0
    
    surface.CreateFont(name, {
        font = fontName,
        size = fontSize,
        weight = fontWeight,
        outline = outline,
        additive = additive,
        antialias = antialias,
        blursize = blur
    })
end

print("[HudResources] Loaded - Resource file parser ready")
