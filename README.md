# 🎮 AI Chaos - Let Chat Control Your Game!

AI Chaos is a fun system that lets Twitch/YouTube chat (or anyone) send commands to your Garry's Mod game using AI. The AI turns natural language like "make everyone tiny" into actual game code!

> **Perfect for streamers** who want to let their audience cause chaos in their game! 💥

---

## 📋 Table of Contents

1. [What You Need](#-what-you-need)
2. [Step 1: Install Prerequisites](#step-1-install-prerequisites)
3. [Step 2: Download AI Chaos](#step-2-download-ai-chaos)
4. [Step 3: Get Your OpenRouter API Key](#step-3-get-your-openrouter-api-key)
5. [Step 4: Run the Brain Server](#step-4-run-the-brain-server)
6. [Step 5: Configure in Browser](#step-5-configure-in-browser)
7. [Step 6: Set Up Garry's Mod](#step-6-set-up-garrys-mod)
8. [Step 7: Make It Public (Optional)](#step-7-make-it-public-optional)
9. [Step 8: Connect Twitch/YouTube (Optional)](#step-8-connect-twitchyoutube-optional)
10. [Testing It Works](#-testing-it-works)
11. [Troubleshooting](#-troubleshooting)
12. [Quick Reference](#-quick-reference)

---

## 📦 What You Need

Before starting, make sure you have:

| Requirement | Why? | Get it here |
|------------|------|-------------|
| **.NET 9.0 SDK** | Runs the Brain server | [Download](https://dotnet.microsoft.com/download) |
| **Garry's Mod** | The game you're controlling | [Steam](https://store.steampowered.com/app/4000/Garrys_Mod/) |
| **OpenRouter API Key** | Powers the AI (free tier available) | [Get one](https://openrouter.ai/keys) |
| **A web browser** | For the control panel | You have one! |

**Optional but recommended:**
- **ngrok** or **bore** - To let people access your server from the internet
- **Twitch/YouTube account** - For chat integration

---

## Step 1: Install Prerequisites

### Install .NET 9.0 SDK

1. Go to [dotnet.microsoft.com/download](https://dotnet.microsoft.com/download)
2. Click "Download .NET SDK x64" (get the **SDK**, not just Runtime)
3. Run the installer
4. **Verify it worked** - Open a terminal/command prompt and type:
   ```bash
   dotnet --version
   ```
   You should see `9.0.xxx` or higher

### For Windows Users
- Download the `.exe` installer
- Double-click to install
- Restart your terminal after installing

### For Mac Users
- Download the `.pkg` installer
- Double-click to install
- Restart your terminal after installing

### For Linux Users
```bash
# Ubuntu/Debian
sudo apt-get update && sudo apt-get install -y dotnet-sdk-9.0

# Fedora
sudo dnf install dotnet-sdk-9.0
```

---

## Step 2: Download AI Chaos

### Option A: Download ZIP (Easiest)
1. Click the green **"Code"** button at the top of this page
2. Click **"Download ZIP"**
3. Extract the ZIP to a folder you'll remember (e.g., `C:\AIChaos` or `~/AIChaos`)

### Option B: Clone with Git
```bash
git clone https://github.com/Xenthio/AIChaos.git
cd AIChaos
```

---

## Step 3: Get Your OpenRouter API Key

OpenRouter is the service that provides AI access. It's free to start!

1. Go to [openrouter.ai](https://openrouter.ai/)
2. Click **"Sign Up"** (you can use Google, GitHub, or email)
3. Once logged in, go to [openrouter.ai/keys](https://openrouter.ai/keys)
4. Click **"Create Key"**
5. Give it a name like "AI Chaos"
6. **Copy the key** - it looks like `sk-or-v1-xxxxxxxxxxxxxxxx`
7. **Save it somewhere** - you'll need it in Step 5!

> 💡 **Tip:** OpenRouter gives you free credits to start. After that, costs depend on the AI model you choose.

---

## Step 4: Run the Brain Server

The "Brain" is the server that receives commands and generates game code.

1. Open a terminal/command prompt
2. Navigate to the AIChaos folder:
   ```bash
   cd path/to/AIChaos/AIChaos.Brain
   ```
3. Run the server:
   ```bash
   dotnet run
   ```

You should see:
```
========================================
  AI Chaos Brain - C# Edition
========================================
  Control Panel: http://localhost:5000/
  Setup: http://localhost:5000/setup
  History: http://localhost:5000/history
========================================
```

🎉 **The Brain is running!** Keep this terminal open.

---

## Step 5: Configure in Browser

1. Open your web browser
2. Go to **http://localhost:5000/setup**
3. **Set an admin password** (first time only) - this protects your setup page
4. Enter your **OpenRouter API Key** from Step 3
5. Choose an AI model (Claude Sonnet is recommended for best results)
6. Click **Save**

> 🔒 The password protects the setup and history pages from public access.

---

## Step 6: Set Up Garry's Mod

The GMod addon is already included! You just need to put it in the right place.

1. Navigate to your GMod addons folder:
   - **Windows:** `C:\Program Files (x86)\Steam\steamapps\common\GarrysMod\garrysmod\addons\`
   - **Mac:** `~/Library/Application Support/Steam/steamapps/common/GarrysMod/garrysmod/addons/`
   - **Linux:** `~/.steam/steam/steamapps/common/GarrysMod/garrysmod/addons/`

2. Copy the entire `lua` folder from AIChaos into a new folder called `AIChaos`:
   ```
   addons/
   └── AIChaos/
       └── lua/
           └── autorun/
               └── ai_chaos_controller.lua
   ```

3. Start Garry's Mod and create a game (singleplayer or host a server)

4. Open the console (`~` key) - you should see:
   ```
   [AI Chaos] Server Initialized!
   [AI Chaos] Polling endpoint: ...
   [AI Chaos] Starting Polling Loop...
   ```

---

## Step 7: Make It Public (Optional)

**If you're only testing locally, skip this step!**

To let others send commands (like Twitch chat), you need a public URL. The easiest way:

### Using ngrok (Recommended)

1. Download ngrok from [ngrok.com/download](https://ngrok.com/download)
2. Create a free account and get your auth token
3. Set up ngrok:
   ```bash
   ngrok authtoken YOUR_TOKEN_HERE
   ```
4. In the Setup page (http://localhost:5000/setup), click **"Start ngrok"**
5. Your public URL will appear - share this with your audience!

### Using bore (No Account Needed)

1. Install bore:
   ```bash
   # If you have Rust/Cargo
   cargo install bore-cli
   
   # Or download from: https://github.com/ekzhang/bore/releases
   ```
2. In the Setup page, click **"Start bore"**
3. Your public URL will appear!

### Other Options

**LocalTunnel** is another free option (requires Node.js):
```bash
npm install -g localtunnel
```
Note: LocalTunnel shows a password page on first access.

> 📖 See [TUNNEL_COMPARISON.md](TUNNEL_COMPARISON.md) for a detailed comparison of all tunnel options.

---

## Step 8: Connect Twitch/YouTube (Optional)

### Twitch Setup

1. Go to [dev.twitch.tv/console](https://dev.twitch.tv/console)
2. Create a new application:
   - **Name:** Something like "AI Chaos"
   - **OAuth Redirect URL:** `http://localhost:5000/api/setup/twitch/callback`
   - **Category:** Game Integration
3. Copy the **Client ID** and **Client Secret**
4. In the Setup page, enter these and click **"Login with Twitch"**
5. Set your channel name and cooldown settings
6. Click **"Start Listening"**

### YouTube Setup

1. Go to [Google Cloud Console](https://console.cloud.google.com/)
2. Create a new project
3. Enable the **YouTube Data API v3**
4. Create OAuth 2.0 credentials:
   - **Application type:** Web Application
   - **Redirect URI:** `http://localhost:5000/api/setup/youtube/callback`
5. Copy the **Client ID** and **Client Secret**
6. In the Setup page, enter these and click **"Login with YouTube"**
7. Enter your live stream's **Video ID** and click **"Start Listening"**

> 📖 See [YOUTUBE_SETUP.md](YOUTUBE_SETUP.md) for detailed YouTube instructions.

---

## 🧪 Testing It Works

1. Make sure:
   - ✅ Brain server is running (terminal shows the banner)
   - ✅ Garry's Mod is running with the addon
   - ✅ You're in a game (not just the main menu)

2. Open **http://localhost:5000/** in your browser

3. Type a command like: **"Make everyone tiny for 10 seconds"**

4. Click **Submit**

5. Watch your game - characters should shrink!

### Example Commands to Try
- "Make everyone tiny"
- "Spawn 5 headcrabs in front of the player"
- "Make the screen shake"
- "Change gravity to moon gravity"
- "Spawn a watermelon above the player"
- "Make everyone move super fast"

---

## 🔧 Troubleshooting

### "dotnet is not recognized"
- Make sure .NET SDK is installed (not just Runtime)
- Restart your terminal after installing
- Try `dotnet --version` to verify

### Brain won't start
- Check if port 5000 is already in use
- Make sure you're in the `AIChaos.Brain` folder
- Try running `dotnet restore` first

### GMod shows "Connection Failed"
- Is the Brain server running?
- Check the URL in `ai_chaos_controller.lua`
- Firewall might be blocking the connection

### Commands not working in game
- Open GMod console (`~`) and look for errors
- Make sure you're actually in a game, not the menu
- Check the History page (http://localhost:5000/history) for errors

### "Invalid API key"
- Double-check your OpenRouter API key
- Make sure you copied the whole key (starts with `sk-or-v1-`)
- Check if you have credits left on OpenRouter

### Tunnel not connecting
- Make sure ngrok/bore is installed
- Check if you ran `ngrok authtoken` first
- Try restarting the Brain and tunnel

---

## 📚 Quick Reference

| URL | What it does |
|-----|--------------|
| http://localhost:5000/ | Control Panel - Send commands here |
| http://localhost:5000/setup | Setup Page - Configure API keys, tunnel, integrations |
| http://localhost:5000/history | History - See past commands, undo them, repeat them |

| Folder/File | Purpose |
|-------------|---------|
| `AIChaos.Brain/` | The server that handles everything |
| `lua/autorun/ai_chaos_controller.lua` | GMod addon that receives commands |
| `ngrok_url.txt` | Created when you start a tunnel |

| Command | What it does |
|---------|--------------|
| `cd AIChaos.Brain && dotnet run` | Start the Brain server |
| `dotnet restore` | Install dependencies if something's broken |
| `dotnet build` | Build without running |

---

## 📖 More Documentation

- [COMMAND_HISTORY_GUIDE.md](COMMAND_HISTORY_GUIDE.md) - How to use the history/undo system
- [TUNNEL_COMPARISON.md](TUNNEL_COMPARISON.md) - Compare ngrok, bore, and other tunnels
- [YOUTUBE_SETUP.md](YOUTUBE_SETUP.md) - Detailed YouTube integration guide
- [NGROK_LAUNCHER_README.md](NGROK_LAUNCHER_README.md) - ngrok auto-launcher details
- [AIChaos.Brain/README.md](AIChaos.Brain/README.md) - Technical details about the Brain

---

## 🙋 Need Help?

1. Check the [Troubleshooting](#-troubleshooting) section above
2. Look at the console/terminal for error messages
3. Check the History page for command errors
4. Open an issue on GitHub!

---

## 📜 License

See the LICENSE file for details.

---

**Have fun causing chaos! 🎉**
