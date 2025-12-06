# Workshop Content Integration

This feature allows the AI to download and spawn models from the Steam Workshop, expanding the range of content available for creative chaos.

## ⚠️ Important Security Notice

Workshop downloads are **disabled by default** for security reasons. Only enable this feature if you understand the risks:

- The AI will be able to download and mount workshop addons
- Downloaded content will be spawned in your game
- Only enable this on trusted installations

## Enabling Workshop Downloads

1. Navigate to the **Dashboard** → **Setup** tab
2. Scroll to the **General Settings** section
3. Check the box labeled **"Allow Workshop Downloads"**
4. Click **"💾 Save General Settings"**

The setting will take effect immediately for new AI requests.

## Available Helper Functions

When workshop downloads are enabled, the AI has access to four new Lua helper functions:

### 1. `GetAllMountedAddonAssets()`

Returns a table with all assets from currently mounted workshop addons, organized by category.

**Use Case:** AI preparation phase to discover what workshop content is available

**Returns:** Table with categories `{ models = {...}, materials = {...}, sounds = {...} }`

**Example:**
```lua
local assets = GetAllMountedAddonAssets()
print("Found " .. #assets.models .. " models, " .. #assets.materials .. " materials, " .. #assets.sounds .. " sounds")
for i, model in ipairs(assets.models) do
    print(i .. ": " .. model)
end
```

**Note:** This scans all currently mounted workshop content without downloading anything new.

### 2. `DownloadAndMountWorkshopAddon(workshopId, callback)`

Downloads and mounts a workshop addon at runtime without spawning anything.

**Parameters:**
- `workshopId` (string): The Steam Workshop ID (can be found in the workshop URL)
- `callback` (function, optional): Called with `callback(success, path)` where success is boolean and path is the download path

**Example:**
```lua
DownloadAndMountWorkshopAddon("WORKSHOP_ID", function(success, path)
    if success then
        print("Addon mounted at: " .. path)
        -- Can now browse assets or spawn models manually
    else
        print("Failed to mount addon")
    end
end)
```

**Features:**
- Automatically checks if workshop downloads are enabled
- Downloads and mounts the addon
- Does not spawn anything - gives you control over what to do next

### 3. `DownloadAndGetWorkshopAssets(workshopId, callback)`

Downloads a workshop addon and returns all its assets organized by category.

**Parameters:**
- `workshopId` (string): The Steam Workshop ID
- `callback` (function, optional): Called with assets table `callback(assets)` where assets is `{ models = {...}, materials = {...}, sounds = {...} }`

**Example:**
```lua
DownloadAndGetWorkshopAssets("WORKSHOP_ID", function(assets)
    if assets then
        print("Found " .. #assets.models .. " models in this addon")
        print("Available models:")
        for i, model in ipairs(assets.models) do
            print(i .. ": " .. model)
        end
        -- Can choose which model to spawn
    end
end)
```

**Features:**
- Downloads and mounts the addon
- Scans for models, materials, and sounds
- Filters out invisible models (gestures, references)
- Returns organized asset list for selective use

### 4. `DownloadAndSpawnWorkshopModel(workshopId, callback)`

Downloads and mounts a workshop addon, then spawns the first valid model found. Excludes gesture models and other invisible models.

**Parameters:**
- `workshopId` (string): The Steam Workshop ID
- `callback` (function, optional): Called with the spawned entity (or nil on failure)

**Example:**
```lua
DownloadAndSpawnWorkshopModel("WORKSHOP_ID", function(ent)
    if IsValid(ent) then
        print("Spawned workshop model!")
        -- Can apply effects to the entity here
        ent:SetColor(Color(255, 0, 0))
    else
        print("Failed to spawn workshop model")
    end
end)
```

**Features:**
- Automatically checks if workshop downloads are enabled
- Downloads and mounts the addon if not already present
- Filters out invisible models (gestures, references)
- Spawns the first valid model in front of the player
- Provides entity reference for further manipulation

## How It Works

### In Single-Shot Mode

When a viewer requests something like "spawn a cool workshop model":

1. The AI can use `GetAllMountedAddonAssets()` to see what's already available
2. Or use `DownloadAndSpawnWorkshopModel()` to download and spawn from a specific workshop ID
3. The model appears in front of the player

### In Interactive/Agentic Mode

The AI can be more intelligent:

1. **Preparation Phase:** Browse available workshop content with `GetAllMountedAddonAssets()` or `DownloadAndGetWorkshopAssets()`
2. **Decision Phase:** Choose the most appropriate model/material/sound based on the request
3. **Execution Phase:** Download and spawn the selected content
4. **Enhancement Phase:** Apply additional effects to the spawned entity

## Filtered Assets

The system automatically filters out non-visible models to prevent confusion:

- Gesture models (animations, not physical models)
- Reference models (skeletal/technical models)
- Physics-only models (`.phys.mdl` files)
- Reference meshes (`.ref.mdl` files)

For materials and sounds, all valid files are included without filtering.

## Example User Requests

With workshop downloads enabled, viewers can request:

- "Spawn a random workshop model"
- "Download and spawn a cool prop from the workshop"
- "Show me what workshop models are available"
- "Spawn a workshop ragdoll and make it dance"

## Safety Features

✅ **Permission Check:** Every workshop operation verifies the setting is enabled

✅ **Default Off:** Feature is disabled by default and requires explicit opt-in

✅ **Model Filtering:** Automatically excludes invisible/problematic models

✅ **Error Handling:** Gracefully handles download failures and invalid IDs

✅ **Callback-based:** Asynchronous operations prevent game freezing

## Troubleshooting

### "Workshop downloads are disabled in settings"

Enable the feature in Dashboard → Setup → General Settings → Allow Workshop Downloads

### Models from wrong addon appearing

The `file.Find()` function in GMod searches across all mounted workshop addons. This is normal behavior. The most recently downloaded addon should provide the first valid model found.

### Workshop download fails

- Verify the workshop ID is valid
- Check that the item is publicly available on Steam Workshop
- Ensure GMod has permission to download from Steam
- Check console for detailed error messages

## Technical Notes

- Workshop IDs can be found in the Steam Workshop URL (e.g., `https://steamcommunity.com/sharedfiles/filedetails/?id=123456`)
- The GMod steamworks API is used for all download operations
- Addons are automatically mounted after successful download
- Downloaded addons persist across game sessions

## Disabling the Feature

To disable workshop downloads:

1. Go to Dashboard → Setup → General Settings
2. Uncheck **"Allow Workshop Downloads"**
3. Click **"💾 Save General Settings"**

Any in-flight download operations will complete, but new requests will be blocked.
