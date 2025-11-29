# Command History & Management System

## Overview
The AI Chaos system now includes comprehensive command history tracking with undo functionality, repeat options, and user controls.

## Features

### 1. **Command History Storage**
- All executed commands are stored with timestamps
- Each command includes:
  - User's original prompt
  - Generated execution code
  - Auto-generated undo code
  - Image context (if applicable)
  - Execution timestamp
  - Unique ID

### 2. **History UI Page**
Access the history page at: `http://127.0.0.1:5000/history`

Features:
- View all previously executed commands (newest first)
- See timestamps and command IDs
- View/hide the generated code for each command
- Auto-refreshes every 5 seconds

### 3. **Undo System**

#### **Regular Undo**
- Each command has AI-generated undo code
- Click "↩ Undo" to execute the pre-generated undo
- Works for most temporary effects

#### **Force Undo** ⚠️
- For stubborn effects that won't stop
- AI generates a comprehensive cleanup script
- Aggressively removes timers, entities, UI elements
- Use when regular undo isn't working

### 4. **Repeat Functionality** 🔁
Perfect for:
- **"Streamer didn't see my donation"** - Repeat without wasting money on regeneration
- **"It got drowned out"** - Re-execute the same effect
- **Testing** - Quickly repeat an effect

The repeat button re-queues the **exact same code** (no AI regeneration needed).

### 5. **User Preferences**

#### **Feed history to AI context**
- When enabled, the last 5 commands are sent to the AI
- Helps AI understand what's already been done
- Can prevent conflicts or create better synergy
- Toggle on/off in the history page

#### **Track command history**
- Master toggle for the entire history system
- Disable if you don't want commands stored
- Turn off by asking "disable command history" in chat

### 6. **Settings**
- **Max History Length**: Default 50 commands (configurable in code)
- **Clear All History**: Button to wipe all stored commands
- **Real-time Updates**: History page auto-refreshes

## How to Use

### For Streamers:
1. Keep history page open on a second monitor
2. Watch for problem effects
3. Quickly undo stubborn effects with one click
4. See what the chat has been doing

### For Chat/Donors:
- If your effect wasn't seen: Click "🔁 Repeat"
- No need to re-donate or re-type
- Your original code runs again instantly

### For Moderators:
- Use "↩ Undo" for quick reversals
- Use "⚠ Force Undo" for persistent problems
- View code to understand what's happening

## API Endpoints

### `/history`
Renders the history UI page

### `/api/history` (GET)
Returns all command history and user preferences

### `/api/repeat` (POST)
```json
{
  "command_id": 5
}
```
Re-executes a previous command

### `/api/undo` (POST)
```json
{
  "command_id": 5
}
```
Executes the undo code for a command

### `/api/force_undo` (POST)
```json
{
  "command_id": 5
}
```
Generates and executes AI-powered comprehensive undo

### `/api/preferences` (POST)
```json
{
  "include_history_in_ai": true,
  "history_enabled": true,
  "max_history_length": 50
}
```
Updates user preferences

### `/api/clear_history` (POST)
Clears all command history

## Code Example

The AI now generates code in this format:

```lua
-- EXECUTION CODE
for _, v in pairs(player.GetAll()) do 
    v:SetModelScale(0.2, 1) 
end
timer.Simple(10, function()
    for _, v in pairs(player.GetAll()) do 
        v:SetModelScale(1, 1) 
    end
end)
---UNDO---
-- UNDO CODE
for _, v in pairs(player.GetAll()) do 
    v:SetModelScale(1, 1) 
end
for k, v in pairs(timer.GetTimers()) do
    if v and string.find(k, "Simple") then timer.Remove(k) end
end
```

The system automatically splits and stores both parts.

## Tips

1. **Keep history enabled** for better AI context awareness
2. **Use repeat** instead of re-typing for failed donations
3. **Force undo** is more aggressive than regular undo
4. **View code** to learn Lua or debug issues
5. **Clear history** periodically to keep things clean

## Future Improvements
- Export history to file
- Search/filter commands
- Favorite commands
- Command categories
- Undo all at once
- Scheduled commands
