# 🎮 Chaos - Let Your Audience Control Your Game!

Chaos is a stream-ready system that lets viewers send creative "Ideas" to control your Garry's Mod game! Using advanced code generation, it turns natural language like "make everyone tiny" into actual game code.

> **Perfect for streamers** who want to create an interactive, chaotic experience with their audience! 💥

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
| **OpenRouter API Key** | Powers the code generation (free tier available) | [Get one](https://openrouter.ai/keys) |
| **A web browser** | For the control panel | You have one! |

**Optional but recommended:**
- **ngrok** or **bore** - To let viewers access your server from the internet
- **YouTube account** - For Super Chat integration ($1 per Idea)

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

OpenRouter is the service that provides code generation access. It's free to start!

1. Go to [openrouter.ai](https://openrouter.ai/)
2. Click **"Sign Up"** (you can use Google, GitHub, or email)
3. Once logged in, go to [openrouter.ai/keys](https://openrouter.ai/keys)
4. Click **"Create Key"**
5. Give it a name like "Chaos"
6. **Copy the key** - it looks like `sk-or-v1-xxxxxxxxxxxxxxxx`
7. **Save it somewhere** - you'll need it in Step 5!

> 💡 **Tip:** OpenRouter gives you free credits to start. After that, costs depend on the model you choose.

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
  Chaos Brain - C# Edition
========================================
  Control Panel: http://localhost:5000/
  Dashboard: http://localhost:5000/dashboard
========================================
```

🎉 **The Brain is running!** Keep this terminal open.

---

## Step 5: Configure in Browser

1. Open your web browser
2. Go to **http://localhost:5000/dashboard**
3. Click on the **"Setup"** tab
4. **Set an admin password** (first time only) - this protects your dashboard
5. Enter your **OpenRouter API Key** from Step 3
6. Choose a code generator model (Claude Sonnet is recommended for best results)
7. Click **Save**

> 🔒 The password protects the dashboard from public access while keeping the main submission page open for viewers.

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

To let viewers send Ideas (like through YouTube Super Chats), you need a public URL. The easiest way:

### Using ngrok (Recommended)

1. Download ngrok from [ngrok.com/download](https://ngrok.com/download)
2. Create a free account and get your auth token
3. Set up ngrok:
   ```bash
   ngrok authtoken YOUR_TOKEN_HERE
   ```
4. In the Dashboard → Setup page, click **"Start ngrok"**
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

### External Hosting (Recommended for Streamers) ⭐

**This is the ideal method for streamers!** Instead of running tunnels on your local machine, host the Brain server on a separate server (VPS, cloud instance, or another PC) and point your GMod installation to it.

**Why this is better for streaming:**
- ✅ **No tunneling software needed** - Direct connection to your server
- ✅ **Better performance** - No tunnel overhead or session limits
- ✅ **Always available** - Server stays up 24/7 even when you're not streaming
- ✅ **Separation of concerns** - Game runs on your PC, Brain runs elsewhere
- ✅ **Easier setup** - Just set one URL in a text file

**How to set it up:**

1. Deploy the Brain server to your VPS/cloud server (AWS, DigitalOcean, etc.)
   ```bash
   # On your server
   cd AIChaos.Brain
   dotnet run --urls "http://0.0.0.0:5000"
   ```
   > ⚠️ **Production tip**: For public deployments, set up a reverse proxy (nginx/caddy) with HTTPS instead of exposing port 5000 directly.

2. Configure network access:
   - **For testing/local network**: Make sure port 5000 is accessible from your gaming PC
   - **For production/public use**: Set up a reverse proxy (nginx/caddy) with HTTPS on ports 80/443, keep port 5000 firewalled to localhost only

3. In your **GMod addons folder** on your gaming PC, create a file:
   ```
   addons/AIChaos/tunnel_url.txt
   ```

4. Put your server's URL in the file (without `/poll`):
   ```
   https://your-server.example.com
   ```
   or if using HTTP (local network/testing only):
   ```
   http://your-server-ip:5000
   ```

5. Restart Garry's Mod - it will automatically connect to your external server!

**Security recommendations:**
- 🔒 **Use HTTPS in production** - Set up a reverse proxy (nginx/caddy) with SSL certificates (use Let's Encrypt for free SSL). The proxy should listen on ports 80/443 and forward to localhost:5000.
- 🔒 **Firewall configuration** - For viewer submissions, keep port 80/443 open; optionally restrict admin dashboard routes (`/dashboard/*`) with IP filtering or basic authentication in your reverse proxy
- 🔒 **Set admin password** - Protect the dashboard from unauthorized access (configured on first visit)
- 🔒 **Monitor the History tab** - Keep an eye on submitted Ideas for abuse

> ⚠️ **Security Notes**:
> - **Local testing**: Binding to 0.0.0.0:5000 is acceptable for local network testing without public access
> - **Production deployment**: Use nginx/caddy with HTTPS on standard ports (80/443) and keep port 5000 firewalled to localhost only
> - **Port configuration**: Only expose 80/443 publicly; the reverse proxy handles SSL and forwards to localhost:5000

**Example cloud providers:**
- **DigitalOcean** - Simple droplets starting at $6/month
- **AWS Lightsail** - Easy VPS hosting
- **Linode** - Affordable cloud instances
- **Oracle Cloud** - Free tier available

> 💡 **Tip**: You can run the Brain on a separate PC in your home network too! Just use the PC's local IP address (e.g., `http://192.168.1.100:5000`).

### Other Options

**LocalTunnel** is another free option (requires Node.js):
```bash
npm install -g localtunnel
```
Note: LocalTunnel shows a password page on first access.

> 📖 See [TUNNEL_COMPARISON.md](TUNNEL_COMPARISON.md) for a detailed comparison of all tunnel options.

---

## Step 8: Connect YouTube (Optional)

### YouTube Super Chat Integration

Chaos features a streamlined YouTube integration with $1 per Idea pricing:

1. Go to Dashboard → **Stream Control** tab
2. Click **"🔗 Login with YouTube"**
3. Authorize the app with your YouTube account
4. Enter your live stream's **Video ID** 
5. Click **"Save Video ID"** (automatically starts listening)

**Viewer Experience:**
- Viewers can donate $1 via Super Chat to send an Idea
- Each dollar = 1 Idea submission
- Credits are "invisible" - balance only shows when clicking profile
- Button shows "Send Chaos" with no price displayed
- When out of credits: "You'll need to donate again to send another"

**For Viewers:**
- Go to your public URL
- Click "Get a Link Code" if not logged in
- Send the code in YouTube chat to link their channel
- Donate via Super Chat ($1 minimum per Idea)
- Credits appear automatically and can be used to submit Ideas

> 📖 See [YOUTUBE_SETUP.md](YOUTUBE_SETUP.md) for detailed YouTube OAuth setup instructions.

---

## 🧪 Testing It Works

1. Make sure:
   - ✅ Brain server is running (terminal shows the banner)
   - ✅ Garry's Mod is running with the addon
   - ✅ You're in a game (not just the main menu)

2. Open **http://localhost:5000/** in your browser

3. Type an Idea like: **"Make everyone tiny for 10 seconds"**

4. Click **Send Chaos**

5. Watch your game - characters should shrink!

### Example Ideas to Try
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
- Check the URL in `chaos_controller.lua`
- Firewall might be blocking the connection

### Ideas not working in game
- Open GMod console (`~`) and look for errors
- Make sure you're actually in a game, not the menu
- Check the History tab in Dashboard for errors

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
| http://localhost:5000/ | Public submission page - Send Ideas here |
| http://localhost:5000/dashboard | Dashboard - Stream Control, Setup, History, Moderation |

**Dashboard Tabs:**
- **Stream Control** - All-in-one streaming hub (default landing page)
  - Queue control with manual blast (Admin only)
  - YouTube video ID & OAuth (Admin only)  
  - Incoming links moderation
  - Refund requests
  - Recent command history with undo/save
- **Setup** - Configure API keys, models, OAuth, tunnels
- **Commands** - Browse and trigger saved payloads
- **History** - Full command history with detailed controls
- **Moderation** - Review and moderate pending submissions
- **Users** - Manage user accounts and balances (Admin only)
- **Testing** - Test mode for development (Admin only)

**Role-Based Access:**
- **Moderators**: Can access Stream Control, Commands, History, Moderation
- **Admins**: Full access to all features including Setup and Users

| Folder/File | Purpose |
|-------------|---------|
| `AIChaos.Brain/` | The server that handles everything |
| `lua/autorun/chaos_controller.lua` | GMod addon that receives commands |
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
- [YOUTUBE_SETUP.md](YOUTUBE_SETUP.md) - Detailed YouTube OAuth integration guide
- [NGROK_LAUNCHER_README.md](NGROK_LAUNCHER_README.md) - ngrok auto-launcher details
- [AIChaos.Brain/README.md](AIChaos.Brain/README.md) - Technical details about the Brain server

## ⚡ StreamReady Features

This version includes the complete **StreamReady Update** with:

- **💵 $1 per Idea pricing** - Simple, transparent economy
- **👻 Invisible economy UX** - Balance hidden from main interface, shown only in profile
- **🎛️ Slot-based queue** - Dynamic pacing (3-10 concurrent slots based on demand)
- **📊 Unified Stream Control** - All-in-one dashboard for streaming
- **🔐 Role-based access** - Moderator vs Admin permissions
- **🔗 Universal URL moderation** - Review all links, not just images
- **📱 Mobile responsive** - Horizontal scrolling tabs for mobile
- **✨ Real-time feedback** - Instant notifications for account linking

### Queue System Details

The slot-based queue replaces traditional FIFO with dynamic pacing:
- **Low volume (0-5 in queue)**: 3 slots → steady "drip feed"
- **High volume (50+ in queue)**: 10 slots → "absolute chaos" mode
- **25-second slot timer**: Independent of effect duration for consistent pacing
- **Manual blast**: Admins can bypass queue limits instantly (1-10 commands)

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
