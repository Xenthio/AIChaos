# Semi-Permanent Code Feature

## Overview

AIChaos now supports semi-permanent code that persists across map changes. This allows the AI to create custom entities, weapons, and other game modifications that remain active even when the map changes.

## How It Works

### 1. Creating Persistent Code

The AI can use the `CreatePersistent()` function to register code that will automatically re-execute on every map load:

```lua
CreatePersistent(name, description, type, code)
```

**Parameters:**
- `name` (string) - Unique identifier for the code (e.g., "chaos_coin", "chaos_blaster")
- `description` (string) - Human-readable description of what the code does
- `type` (string) - Type of persistent code: "entity", "weapon", "generic", or "gamemode"
- `code` (string) - The actual Lua code to execute

**Returns:** `boolean` - true if successful, false otherwise

### 2. Persistence Mechanism

- Persistent code is stored on the C# server in `persistent_code/persistent_code.json`
- On every map load, GMod addon calls `/persistent-code` API endpoint
- Server sends combined script of all active persistent code
- GMod executes the script to re-register entities/weapons

### 3. Important Notes

- **Only definitions persist**, not spawned instances
- After creating persistent code, you still need to spawn instances
- Use descriptive names (e.g., "chaos_coin" not "ent1")
- Persistent code can be managed via the Admin Dashboard → Persistent Code tab

## Example: Custom Collectible Coin Entity

```lua
-- Prompt: "Create a custom collectible coin entity that gives players points"

-- Step 1: Create the persistent entity definition
CreatePersistent("chaos_coin", "Spinning collectible coin that gives +10 points", "entity", [[
    -- Define the entity
    local ENT = {}
    ENT.Type = "anim"
    ENT.Base = "base_gmodentity"
    ENT.PrintName = "Chaos Coin"
    ENT.Category = "AI Chaos"
    ENT.Spawnable = true
    ENT.AdminOnly = false
    ENT.Author = "AI Chaos"
    
    -- Server-side initialization
    function ENT:Initialize()
        self:SetModel("models/props_combine/combine_lock01.mdl")
        self:PhysicsInit(SOLID_VPHYSICS)
        self:SetMoveType(MOVETYPE_VPHYSICS)
        self:SetSolid(SOLID_VPHYSICS)
        self:SetUseType(SIMPLE_USE)
        self:SetColor(Color(255, 215, 0)) -- Gold color
        
        local phys = self:GetPhysicsObject()
        if IsValid(phys) then
            phys:Wake()
        end
    end
    
    function ENT:Use(activator, caller)
        if activator:IsPlayer() then
            activator:ChatPrint("You collected a coin! +10 points")
            self:EmitSound("items/battery_pickup.wav")
            self:Remove()
        end
    end
    
    function ENT:Think()
        -- Rotate the coin for visual effect
        local ang = self:GetAngles()
        ang:RotateAroundAxis(ang:Up(), 2)
        self:SetAngles(ang)
        self:NextThink(CurTime() + 0.01)
        return true
    end
    
    -- Register the entity with GMod
    scripted_ents.Register(ENT, "chaos_coin")
]])

-- Step 2: Spawn some coins for immediate gameplay
local ply = Entity(1)
for i = 1, 5 do
    local coin = ents.Create("chaos_coin")
    if IsValid(coin) then
        local angle = (360 / 5) * i
        local rad = math.rad(angle)
        local distance = 200
        local pos = ply:GetPos() + Vector(math.cos(rad) * distance, math.sin(rad) * distance, 50)
        coin:SetPos(pos)
        coin:Spawn()
    end
end

print("Created chaos_coin entity - will persist across map changes!")
print("Spawned 5 coins around the player")
```

## Example: Custom Energy Blaster Weapon

```lua
-- Prompt: "Create a custom energy blaster weapon"

-- Step 1: Create the persistent weapon definition
CreatePersistent("chaos_blaster", "Energy blaster that shoots explosive projectiles", "weapon", [[
    local SWEP = {}
    SWEP.PrintName = "Chaos Blaster"
    SWEP.Author = "AI Chaos"
    SWEP.Category = "AI Chaos"
    SWEP.Spawnable = true
    SWEP.AdminOnly = false
    
    SWEP.Primary.ClipSize = 20
    SWEP.Primary.DefaultClip = 20
    SWEP.Primary.Automatic = true
    SWEP.Primary.Ammo = "Pistol"
    SWEP.Primary.Delay = 0.15
    
    SWEP.Secondary.ClipSize = -1
    SWEP.Secondary.DefaultClip = -1
    SWEP.Secondary.Automatic = false
    SWEP.Secondary.Ammo = "none"
    
    SWEP.Weight = 5
    SWEP.AutoSwitchTo = false
    SWEP.AutoSwitchFrom = false
    
    SWEP.Slot = 2
    SWEP.SlotPos = 1
    SWEP.DrawAmmo = true
    SWEP.DrawCrosshair = true
    
    SWEP.ViewModel = "models/weapons/c_pistol.mdl"
    SWEP.WorldModel = "models/weapons/w_pistol.mdl"
    
    function SWEP:Initialize()
        self:SetHoldType("pistol")
    end
    
    function SWEP:PrimaryAttack()
        if not self:CanPrimaryAttack() then return end
        
        self:EmitSound("Weapon_Pistol.Single")
        self:TakePrimaryAmmo(1)
        
        local ply = self:GetOwner()
        if not IsValid(ply) then return end
        
        -- Get aim trace
        local tr = ply:GetEyeTrace()
        
        -- Create explosion effect at hit point
        local effectdata = EffectData()
        effectdata:SetOrigin(tr.HitPos)
        effectdata:SetScale(0.5)
        util.Effect("Explosion", effectdata)
        
        -- Apply damage
        if IsValid(tr.Entity) then
            tr.Entity:TakeDamage(15, ply, self)
        end
        
        -- Knockback effect
        if tr.Hit then
            local phys = tr.Entity:GetPhysicsObject()
            if IsValid(phys) then
                local force = (tr.HitPos - ply:GetPos()):GetNormalized() * 500
                phys:ApplyForceCenter(force)
            end
        end
        
        self:SetNextPrimaryFire(CurTime() + self.Primary.Delay)
    end
    
    function SWEP:SecondaryAttack()
        -- No secondary attack
    end
    
    -- Register the weapon with GMod
    weapons.Register(SWEP, "chaos_blaster")
]])

-- Step 2: Give the weapon to the player
local ply = Entity(1)
ply:Give("chaos_blaster")
ply:SelectWeapon("chaos_blaster")

print("Created chaos_blaster weapon - will persist across map changes!")
print("Given weapon to player")
```

## Example: Generic Persistent Hook

```lua
-- Prompt: "Create a system that randomly changes player speed every 30 seconds"

CreatePersistent("speed_randomizer", "Randomly changes player speed every 30 seconds", "generic", [[
    -- Create persistent timer that adjusts speed
    timer.Create("AI_Chaos_SpeedRandomizer", 30, 0, function()
        for _, ply in pairs(player.GetAll()) do
            local speed = math.random(200, 600)
            ply:SetRunSpeed(speed)
            ply:SetWalkSpeed(speed * 0.5)
            ply:ChatPrint("Your speed has been randomly set to " .. speed .. "!")
        end
    end)
    
    print("Speed randomizer timer created - will persist across map changes")
]])

-- Trigger immediately
for _, ply in pairs(player.GetAll()) do
    local speed = math.random(200, 600)
    ply:SetRunSpeed(speed)
    ply:SetWalkSpeed(speed * 0.5)
    ply:ChatPrint("Speed randomizer activated! Your speed is now " .. speed)
end
```

## Admin Management

Administrators can manage persistent code via the Dashboard:

1. Navigate to **Dashboard** → **🔮 Persistent Code**
2. View all persistent code entries with their details
3. **Deactivate** entries to stop them from loading (soft delete)
4. **Reactivate** deactivated entries
5. **Delete** entries permanently
6. **Show Code** to view the full Lua code

## Technical Details

### File Locations

**C# Side:**
- `AIChaos.Brain/Models/PersistentCode.cs` - Data models
- `AIChaos.Brain/Services/PersistentCodeService.cs` - Management service
- `AIChaos.Brain/Controllers/ChaosController.cs` - API endpoints
- `persistent_code/persistent_code.json` - Storage file

**Lua Side:**
- `lua/autorun/ai_chaos_controller.lua` - Contains `CreatePersistent()` and `LoadPersistentCode()`

### API Endpoints

- `GET/POST /persistent-code` - Returns combined script of all active persistent code
- `POST /persistent-code/create` - Creates new persistent code entry

### Execution Flow

1. **On map load**: GMod calls `LoadPersistentCode()` → fetches `/persistent-code` → executes combined script
2. **When AI creates code**: AI calls `CreatePersistent()` → sends to `/persistent-code/create` → saves to JSON
3. **On next map load**: New code is automatically included in combined script

## Safety Considerations

- Persistent code is executed with full server privileges
- Admin review is recommended before allowing AI to create persistent code
- Use the dashboard to review and manage all persistent code
- Deactivate problematic code immediately via dashboard
- Consider backup of `persistent_code.json` before major changes

## Limitations

- Only the **definition** persists, not spawned instances
- Maximum recommended: ~50 active persistent entries (for performance)
- Code is re-executed on every map load (keep definitions lightweight)
- No automatic cleanup of old entities/weapons
- Admin must manually manage persistent code via dashboard

## Future Enhancements

Potential improvements:
- Automatic code validation before persistence
- Versioning system for persistent code
- Category/tag system for organization
- Export/import of persistent code collections
- Automatic cleanup of unused persistent code
- Code diff viewer for updates
