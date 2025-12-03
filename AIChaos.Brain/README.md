# Chaos Brain (C# Edition)

The C# server for Chaos - receives "Ideas" from viewers and generates Lua code for Garry's Mod.

## Features

- 🤖 **Code Generation** - Uses OpenRouter API to generate Lua code from natural language
- 🎬 **YouTube Integration** - OAuth login and Super Chat processing for monetized Ideas
- 💰 **Invisible Economy** - $1 per Idea with hidden balance UX
- 🎮 **Web Control Panel** - Public submission interface for viewers
- 📊 **Unified Dashboard** - Stream Control, Setup, History, Moderation in one place
- 🎛️ **Slot-based Queue** - Dynamic concurrent execution (3-10 slots based on demand)
- 📜 **Command History** - Track, repeat, undo, and save Ideas as reusable payloads
- 🔒 **Role-Based Access** - Moderator and Admin permission levels
- 🌐 **Built-in Tunnel Support** - Start ngrok or bore directly from the UI
- 🔐 **Password Protection** - Dashboard access protected by admin password

## Quick Start

### Prerequisites

- [.NET 9.0 SDK](https://dotnet.microsoft.com/download) or later
- An [OpenRouter](https://openrouter.ai/) API key
- (Optional) ngrok or LocalTunnel for public access

### Running the Brain

```bash
cd AIChaos.Brain
dotnet run
```

The server will start on `http://localhost:5000`

Open your browser to:
- `http://localhost:5000/` - Public submission page (viewers use this)
- `http://localhost:5000/dashboard` - Dashboard (password protected)
  - **Stream Control** - All-in-one streaming hub (default tab)
  - **Setup** - Configuration and OAuth
  - **History** - Full command history
  - **Moderation** - Review submissions
  - **Commands** - Saved payloads
  - **Users** - Account management (Admin only)
  - **Testing** - Development mode (Admin only)

## Setup Guide

### 1. Set Admin Password

On first visit to `/dashboard`, you'll be prompted to set an admin password. This protects the dashboard from public access.

### 2. OpenRouter API (Required)

1. Go to [openrouter.ai/keys](https://openrouter.ai/keys)
2. Create an account and generate an API key
3. Enter the API key in the Setup tab
4. Choose your preferred code generator model

### 3. Tunnel Configuration (For Public Access)

The Setup page includes built-in tunnel management:

**ngrok:**
1. (Optional) Get an auth token from [ngrok.com](https://ngrok.com/download)
2. Click "Start ngrok" in the Setup tab
3. The public URL will be displayed and auto-configured in the Lua file

**bore:**
1. Click "Start bore" in the Setup tab
2. No account needed - instant public URL

### 4. YouTube Integration (Optional but Recommended)

**Quick Setup from Stream Control:**
1. Go to **Dashboard → Stream Control** tab
2. Click **"🔗 Login with YouTube"**
3. Authorize the app with your YouTube account
4. Enter your live stream's **Video ID**
5. Click **"Save Video ID"** (automatically starts listening)

**Full OAuth Setup:**
1. Go to [Google Cloud Console](https://console.cloud.google.com/)
2. Create a new project
3. Enable the YouTube Data API v3
4. Create OAuth 2.0 credentials (Web Application)
5. Set the redirect URI to: `http://localhost:5000/api/setup/youtube/callback`
6. Copy the Client ID and Client Secret to the Setup tab
7. Complete OAuth flow via "Login with YouTube" button

**How It Works:**
- Viewers donate $1 via Super Chat to get credits
- Each dollar = 1 Idea submission
- Pending credits auto-transfer when viewer links their channel
- Balance is "invisible" - only shown in profile modal

## GMod Setup

The GMod addon in the `lua/` folder needs to connect to this brain. When you start a tunnel, the Lua file is automatically updated with the correct URL.

Manually update `lua/autorun/chaos_controller.lua` if needed.

## StreamReady Features

### Slot-Based Queue System

Replaces traditional FIFO with dynamic concurrent execution:

```csharp
// QueueSlotService manages execution pacing
- Low volume (0-5 in queue): 3 slots → "drip feed" experience
- Medium volume (6-20): 4-6 slots
- High volume (50+): 10 slots → "absolute chaos" mode
```

**Benefits:**
- Prevents overwhelming the game with too many simultaneous effects
- Scales automatically based on demand
- 25-second slot timer independent of effect duration
- Manual blast bypasses limits (1-10 commands instantly)

### Invisible Economy ($1 per Idea)

- **Public interface**: No visible prices or balances
- **Button states**: "Send Chaos" → "Submitting..." → "You'll need to donate again"
- **Profile modal**: Balance visible when clicking username
- **Admin dashboard**: Full financial transparency
- **Pending credits**: Automatic transfer when viewer links channel

### Role-Based Access Control

**Moderators can access:**
- Stream Control (limited panels)
- Commands
- History
- Moderation

**Admins get everything:**
- Full Stream Control (queue control, stream settings)
- Setup & configuration
- Users management
- Testing mode

## API Endpoints

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/` | GET | Public submission page |
| `/dashboard` | GET | Unified dashboard (password protected) |
| `/api/chaos/poll` | GET | GMod polls for commands (slot-based) |
| `/api/chaos/trigger` | POST | Submit an Idea |
| `/api/chaos/undo/{id}` | POST | Undo a specific command |
| `/api/history` | GET | Get command history |
| `/api/queue/status` | GET | Get queue and slot status |
| `/api/queue/blast` | POST | Manual blast (bypass slots) |
| `/api/setup/youtube/callback` | GET | YouTube OAuth callback |
| `/api/setup/status` | GET | Get setup status |
| `/api/setup/models` | GET | Get available code generator models |
| `/api/setup/tunnel/ngrok/start` | POST | Start ngrok tunnel |
| `/api/setup/tunnel/bore/start` | POST | Start bore tunnel |
| `/api/setup/tunnel/stop` | POST | Stop current tunnel |

## Development

```bash
# Restore dependencies
dotnet restore

# Build
dotnet build

# Run in development mode
dotnet run

# Publish for production
dotnet publish -c Release
```

## Troubleshooting

### "Failed to connect to brain"
- Make sure the brain is running on port 5000
- Check firewall settings

### "Invalid API key"
- Verify OpenRouter API key is correct
- Check if you have credits remaining on OpenRouter

### YouTube "Invalid video ID"
- The stream must be actively live
- Use the video ID from the URL, not the channel ID
- Make sure you're authenticated with the correct account

### YouTube "Unauthorized" error
- OAuth token may have expired - click "Login with YouTube" again
- Verify OAuth credentials in Google Cloud Console
- Check redirect URI matches: `http://localhost:5000/api/setup/youtube/callback`

### Tunnel not starting
- ngrok: Make sure ngrok is installed and in PATH
- bore: Make sure bore is installed (`cargo install bore-cli`)

### Queue not processing
- Check Dashboard → Stream Control for slot status
- Verify GMod is polling (check console logs)
- Try Manual Blast to bypass queue limits

## License

See the main repository for license information.
