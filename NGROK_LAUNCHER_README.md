# ngrok Auto-Launcher

This launcher automatically starts ngrok, gets your public URL, and configures everything for you.

## Quick Start

```bash
python start_with_ngrok.py
```

## What It Does

1. ✅ Checks if ngrok is installed
2. ✅ Starts ngrok tunnel on port 5000
3. ✅ Gets your public ngrok URL automatically
4. ✅ Saves URL to `ngrok_url.txt`
5. ✅ Updates `ai_chaos_controller.lua` with the URL
6. ✅ Starts `brain.py`
7. ✅ Cleans up ngrok when you stop

## Installation

### Install ngrok

**Windows:**
```bash
winget install ngrok
```

**macOS:**
```bash
brew install ngrok
```

**Linux:**
```bash
snap install ngrok
```

**Manual:**
1. Download from https://ngrok.com/download
2. Extract `ngrok.exe` (or `ngrok` on Linux/Mac)
3. Add to PATH or place in the AIChaos folder

### First Time Setup

1. Sign up at https://ngrok.com (free account)
2. Get your auth token from the dashboard
3. Run: `ngrok authtoken YOUR_TOKEN_HERE`

## Usage

1. Run the launcher:
   ```bash
   python start_with_ngrok.py
   ```

2. Wait for it to display your URL:
   ```
   Public URL:  https://abc123.ngrok-free.app
   Web UI:      https://abc123.ngrok-free.app/
   History:     https://abc123.ngrok-free.app/history
   ```

3. Restart Garry's Mod to load the updated Lua script

4. Share the public URL with your viewers!

## Features

### Automatic Configuration
- No manual URL editing needed
- Lua script reads from `ngrok_url.txt` automatically
- Fallback to hardcoded URL if file not found

### Clean Shutdown
- Press Ctrl+C to stop
- Automatically stops both ngrok and brain.py
- No orphaned processes

### Cross-Platform
- Works on Windows, macOS, and Linux
- Multiple launcher options for convenience

## Troubleshooting

### "ngrok is not installed"
- Make sure ngrok is installed and in your PATH
- Try running `ngrok version` in terminal
- If not in PATH, place `ngrok.exe` in the AIChaos folder

### "Could not get ngrok URL"
- ngrok might not have started properly
- Check http://localhost:4040 for the ngrok dashboard
- Make sure you've run `ngrok authtoken` first

### Lua script not updating
- Check that the file exists at: `lua/autorun/ai_chaos_controller.lua`
- Make sure you have write permissions
- Manually check the `SERVER_URL` line in the Lua file

### ngrok tunnel closes after 2 hours
- Free ngrok tunnels expire after 2 hours
- Restart the launcher to get a new URL
- Consider upgrading to ngrok Pro for persistent URLs

## Manual Configuration

If the auto-launcher doesn't work, you can manually:

1. Start ngrok:
   ```bash
   ngrok http 5000
   ```

2. Copy the HTTPS URL from the ngrok output

3. Edit `lua/autorun/ai_chaos_controller.lua`:
   ```lua
   local SERVER_URL = "https://your-url-here.ngrok-free.app/poll"
   ```

4. Start brain.py:
   ```bash
   python brain.py
   ```

## Advanced Usage

### Custom Port
Edit the launcher and change:
```python
NGROK_PORT = 5000  # Change to your desired port
```

## Files Created

- `ngrok_url.txt` - Your current ngrok URL (auto-generated)
- This file is read by the Lua script on GMod startup

## Security Notes

- ngrok URLs are public - anyone with the URL can access your brain
- Consider adding authentication if running publicly
- Free ngrok shows a warning page on first visit (users must click "Visit Site")
- URLs change each time you restart ngrok (unless you have ngrok Pro)

### Keep ngrok Running
To keep ngrok running after closing brain.py, comment out the cleanup in the launcher.

### Persistent URLs
Sign up for ngrok Pro to get:
- Custom subdomains
- No 2-hour limit
- No warning page
- Reserved domains
