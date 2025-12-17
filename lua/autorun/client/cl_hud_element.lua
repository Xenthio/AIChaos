if SERVER then return end

-- ============================================================================
--  CHudElement - Port of Source SDK 2013's CHudElement base class
--  
--  This provides the same lifecycle and visibility management as the native
--  C++ CHudElement, allowing for proper HUD element management.
-- ============================================================================

CHudElement = {}
CHudElement.__index = CHudElement

-- Global registry of all HUD elements
CHudElement.Registry = CHudElement.Registry or {}
CHudElement.RenderGroups = CHudElement.RenderGroups or {}

-- HIDEHUD flags (from shareddefs.h)
HIDEHUD_WEAPONSELECTION = 1     -- Hide ammo/weapon selection
HIDEHUD_FLASHLIGHT = 2          -- Hide flashlight
HIDEHUD_ALL = 4                 -- Hide all HUD elements
HIDEHUD_HEALTH = 8              -- Hide health & armor
HIDEHUD_PLAYERDEAD = 16         -- Hide when player is dead
HIDEHUD_NEEDSUIT = 32           -- Hide when player doesn't have HEV suit
HIDEHUD_MISCSTATUS = 64         -- Hide miscellaneous status
HIDEHUD_CHAT = 128              -- Hide chat
HIDEHUD_CROSSHAIR = 256         -- Hide crosshair
HIDEHUD_VEHICLE_CROSSHAIR = 512 -- Hide vehicle crosshair
HIDEHUD_INVEHICLE = 1024        -- Hide when in vehicle
HIDEHUD_BONUS_PROGRESS = 2048   -- Hide bonus progress

-- ============================================================================
--  CHudElement Constructor
-- ============================================================================

function CHudElement:New(elementName)
    local obj = setmetatable({}, self)
    
    obj.m_pElementName = elementName
    obj.m_bActive = true
    obj.m_iHiddenBits = 0
    obj.m_bNeedsRemove = false
    obj.m_bIsParentedToClientDLLRootPanel = true
    obj.m_HudRenderGroups = {}
    obj.m_nRenderGroupPriority = 0
    
    -- Register in global list
    CHudElement.Registry[elementName] = obj
    
    return obj
end

-- ============================================================================
--  Lifecycle Methods
-- ============================================================================

-- Called when the HUD is initialized (whenever the DLL is loaded)
function CHudElement:Init()
    -- Override in derived classes
end

-- Called whenever the video mode changes, and whenever Init() would be called
function CHudElement:VidInit()
    -- Override in derived classes
end

-- Called whenever a new level is starting
function CHudElement:LevelInit()
    -- Override in derived classes
end

-- Called whenever a level is finishing
function CHudElement:LevelShutdown()
    -- Override in derived classes
end

-- Called whenever the hud receives "reset" message (usually on respawn)
function CHudElement:Reset()
    -- Override in derived classes
end

-- Called once per frame for visible elements before general key processing
function CHudElement:ProcessInput()
    -- Override in derived classes
end

-- ============================================================================
--  Visibility Methods
-- ============================================================================

-- Return true if this hud element should be visible in the current hud state
function CHudElement:ShouldDraw()
    if not self.m_bActive then
        return false
    end
    
    local ply = LocalPlayer()
    if not IsValid(ply) then
        return false
    end
    
    -- Check hidden bits
    if self.m_iHiddenBits ~= 0 then
        -- HIDEHUD_PLAYERDEAD
        if bit.band(self.m_iHiddenBits, HIDEHUD_PLAYERDEAD) ~= 0 and not ply:Alive() then
            return false
        end
        
        -- HIDEHUD_NEEDSUIT
        -- Note: In GMod, we approximate "has suit" by checking if player has ever had armor
        -- A more accurate check would require gamemode-specific logic
        if bit.band(self.m_iHiddenBits, HIDEHUD_NEEDSUIT) ~= 0 then
            -- Only hide if player has never had armor (rough approximation)
            if ply:GetMaxArmor() == 0 then
                return false
            end
        end
        
        -- HIDEHUD_INVEHICLE
        if bit.band(self.m_iHiddenBits, HIDEHUD_INVEHICLE) ~= 0 and ply:InVehicle() then
            return false
        end
    end
    
    return true
end

function CHudElement:IsActive()
    return self.m_bActive
end

function CHudElement:SetActive(bActive)
    self.m_bActive = bActive
end

function CHudElement:SetHiddenBits(iBits)
    self.m_iHiddenBits = iBits
end

function CHudElement:GetName()
    return self.m_pElementName
end

-- ============================================================================
--  Render Group Methods
-- ============================================================================

function CHudElement:RegisterForRenderGroup(pszName)
    if not CHudElement.RenderGroups[pszName] then
        CHudElement.RenderGroups[pszName] = {
            bHidden = false,
            elements = {}
        }
    end
    
    table.insert(self.m_HudRenderGroups, pszName)
    table.insert(CHudElement.RenderGroups[pszName].elements, self)
end

function CHudElement:UnregisterForRenderGroup(pszGroupName)
    for i, name in ipairs(self.m_HudRenderGroups) do
        if name == pszGroupName then
            table.remove(self.m_HudRenderGroups, i)
            break
        end
    end
    
    if CHudElement.RenderGroups[pszGroupName] then
        for i, elem in ipairs(CHudElement.RenderGroups[pszGroupName].elements) do
            if elem == self then
                table.remove(CHudElement.RenderGroups[pszGroupName].elements, i)
                break
            end
        end
    end
end

function CHudElement:GetRenderGroupPriority()
    return self.m_nRenderGroupPriority
end

function CHudElement:SetRenderGroupPriority(priority)
    self.m_nRenderGroupPriority = priority
end

-- ============================================================================
--  Global Functions
-- ============================================================================

function CHudElement.GetElementByName(name)
    return CHudElement.Registry[name]
end

function CHudElement.ResetAll()
    for _, elem in pairs(CHudElement.Registry) do
        elem:Reset()
    end
end

function CHudElement.InitAll()
    for _, elem in pairs(CHudElement.Registry) do
        elem:Init()
    end
end

function CHudElement.VidInitAll()
    for _, elem in pairs(CHudElement.Registry) do
        elem:VidInit()
    end
end

function CHudElement.LevelInitAll()
    for _, elem in pairs(CHudElement.Registry) do
        elem:LevelInit()
    end
end

function CHudElement.LevelShutdownAll()
    for _, elem in pairs(CHudElement.Registry) do
        elem:LevelShutdown()
    end
end

-- ============================================================================
--  Hooks for automatic lifecycle management
-- ============================================================================

hook.Add("InitPostEntity", "CHudElement_Init", function()
    CHudElement.InitAll()
end)

hook.Add("OnScreenSizeChanged", "CHudElement_VidInit", function()
    CHudElement.VidInitAll()
end)

-- Level init/shutdown detection
local currentMap = game.GetMap()
hook.Add("Think", "CHudElement_LevelDetection", function()
    local newMap = game.GetMap()
    if newMap ~= currentMap then
        CHudElement.LevelShutdownAll()
        currentMap = newMap
        timer.Simple(0.1, function()
            CHudElement.LevelInitAll()
        end)
    end
end)

-- Player spawn detection for Reset
hook.Add("OnPlayerSpawn", "CHudElement_Reset", function(ply)
    if ply == LocalPlayer() then
        CHudElement.ResetAll()
    end
end)

print("[CHudElement] Loaded - Source SDK 2013 HUD Element System")
