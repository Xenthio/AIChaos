# Tunnel Solutions - Choose the Best for You!

## Quick Comparison

| Feature | Bore ⭐ | LocalTunnel | ngrok | Cloudflare |
|---------|--------|-------------|-------|------------|
| **Account Required** | ❌ No | ❌ No | ✅ Yes | ✅ Yes |
| **Password Page** | ❌ No | ✅ Yes (IP) | ✅ Yes (free) | ❌ No |
| **Installation** | `cargo install bore-cli` | `npm install -g localtunnel` | Download | Download |
| **Ease of Use** | ⭐⭐⭐⭐⭐ | ⭐⭐⭐ | ⭐⭐⭐⭐ | ⭐⭐⭐ |
| **Speed** | Excellent | Good | Excellent | Excellent |
| **Reliability** | Excellent | Good | Excellent | Excellent |
| **Time Limit** | None | None | 2 hours (free) | None |
| **Best For** | Everything! | (not recommended) | Professional | Long-term |

## 🌟 NEW RECOMMENDATION: Bore (bore.pub)

### Why Bore?
- ✅ **No signup required** - Zero configuration
- ✅ **No password pages** - Direct access
- ✅ **No time limits** - Run as long as you want
- ✅ **Fast and reliable** - Modern Rust implementation
- ✅ **Open source** - Trustworthy and transparent
- ✅ **Clean URLs** - No weird subdomains

### Quick Start
```bash
# Install (one time)
cargo install bore-cli

# Or download binary from:
# https://github.com/ekzhang/bore/releases

# Run the launcher
python start_with_bore.py
```

### Pros:
- No account needed
- No password pages
- Fast and modern
- Reliable connection
- Clean implementation

### Cons:
- Requires Rust/Cargo to install (or manual binary download)
- Less well-known than ngrok

---

## 📦 LocalTunnel (Now NOT Recommended)

### Why LocalTunnel?
- ✅ **No signup required** - Just install and run
- ✅ **No time limits** - Keep it running as long as you want
- ✅ **Free forever** - No paid tiers
- ✅ **Easy to use** - One command to start
- ✅ **Auto-installer** - Our launcher can install it for you

### Quick Start
```bash
# Install (one time)
npm install -g localtunnel

# Run the launcher
python start_with_localtunnel.py
```

### Pros:
- No account creation needed
- No authentication tokens
- No time limits
- Simple and straightforward

### Cons:
- May show a "Continue to LocalTunnel" page (one click)
- Random subdomain each time (unless you specify)
- Slightly less reliable than ngrok

---

## 🔧 ngrok (Most Popular)

### Why ngrok?
- ⭐ Most stable and reliable
- ⭐ Best performance
- ⭐ Professional features
- ⭐ Great documentation

### Quick Start
```bash
# 1. Sign up at ngrok.com (required)
# 2. Get your auth token
# 3. Run:
ngrok authtoken YOUR_TOKEN

# 4. Run the launcher
python start_with_ngrok.py
```

### Pros:
- Very reliable
- Fast servers worldwide
- Great web interface
- Professional support

### Cons:
- ❌ Requires free account signup
- ❌ Need to set auth token
- ❌ 2-hour session limit (free tier)
- ❌ Shows warning page on free tier

### Free Tier Limits:
- 1 online ngrok process
- 4 tunnels/ngrok process
- 40 connections/minute
- Sessions expire after 2 hours

---

## ☁️ Cloudflare Tunnel (Advanced)

### Why Cloudflare?
- Best for permanent deployments
- No session limits
- Fast CDN
- Free custom domains

### Setup (More Complex)
```bash
# 1. Install cloudflared
# 2. Login: cloudflared tunnel login
# 3. Create tunnel: cloudflared tunnel create aichaos
# 4. Configure DNS
# 5. Run tunnel
```

### Pros:
- No time limits
- No warning pages
- Free custom domains
- Excellent performance
- CDN caching

### Cons:
- ❌ More complex setup
- ❌ Requires Cloudflare account
- ❌ Need to configure DNS
- ❌ Overkill for testing

---

## 🚀 What Should I Use?

### For Everything (Best Choice):
**→ Use Bore** (`start_with_bore.py`)
- No account needed
- No password pages
- Fast and reliable
- Perfect for streaming and testing

### If Bore Install Fails:
**→ Use ngrok** (`start_with_ngrok.py`)
- Requires one-time signup
- More reliable than LocalTunnel
- No IP password nonsense

### For Permanent 24/7 Hosting:
**→ Use Cloudflare Tunnel**
- Set it and forget it
- No session limits
- Professional solution

---

## Installation Guides

### Bore ⭐ Recommended
```bash
# Option 1: Via Cargo (Rust)
# Install Rust first: https://rustup.rs/
cargo install bore-cli

# Option 2: Download binary
# https://github.com/ekzhang/bore/releases
# Extract and add to PATH

# Verify
bore --version
```

### LocalTunnel (Not Recommended - has password page)
```bash
# Requires Node.js (https://nodejs.org/)
npm install -g localtunnel

# Verify
lt --version
```

### ngrok
```bash
# Windows (with winget)
winget install ngrok

# macOS
brew install ngrok

# Linux
snap install ngrok

# Or download: https://ngrok.com/download

# Setup (one time)
ngrok authtoken YOUR_TOKEN_FROM_DASHBOARD
```

### Cloudflare
```bash
# Windows
# Download from: https://developers.cloudflare.com/cloudflare-one/connections/connect-apps/install-and-setup/installation/

# macOS
brew install cloudflare/cloudflare/cloudflared

# Linux
wget https://github.com/cloudflare/cloudflared/releases/latest/download/cloudflared-linux-amd64
sudo mv cloudflared-linux-amd64 /usr/local/bin/cloudflared
sudo chmod +x /usr/local/bin/cloudflared
```

---

## Our Launchers

We provide Python launchers for the best cross-platform experience:

### start_with_bore.py ⭐⭐⭐ BEST CHOICE
```bash
python start_with_bore.py
```
- No account required
- No password page
- Fast and reliable
- **Perfect for everyone!**

### start_with_ngrok.py - Good Alternative
```bash
python start_with_ngrok.py
```
- Requires ngrok account (free)
- Very reliable
- No password pages after signup
- Good if Bore install fails

### start_with_localtunnel.py - Not Recommended
```bash
python start_with_localtunnel.py
```
- Has annoying IP password page
- Less reliable
- Only use if other options fail

### Manual Setup (any tunnel)
1. Start your tunnel manually
2. Copy the URL
3. Edit `lua/autorun/ai_chaos_controller.lua`
4. Set `SERVER_URL = "https://your-url/poll"`
5. Start `brain.py`

---

## Troubleshooting

### LocalTunnel
**"npm not found"**
- Install Node.js from https://nodejs.org/
- Node.js includes npm

**"Continue to LocalTunnel" page**
- This is normal for LocalTunnel
- Just click "Continue" once
- Not a security issue

**Tunnel keeps disconnecting**
- Try restarting the launcher
- Check your internet connection
- Consider using ngrok instead

### ngrok
**"Account required"**
- Sign up at https://ngrok.com (free)
- Get auth token from dashboard
- Run: `ngrok authtoken YOUR_TOKEN`

**"Session expired"**
- Free tier has 2-hour limit
- Restart the launcher to get new session
- Or upgrade to ngrok Pro

**"Warning page"**
- Free tier shows this
- Users must click "Visit Site"
- Paid tier removes this

---

## Security Considerations

### All Tunnels:
- ⚠️ Your brain API is publicly accessible
- ⚠️ Anyone with the URL can send commands
- ⚠️ Brain has built-in safety filters (changelevel blocking, etc.)

### Recommendations:
1. Don't share the URL publicly unless streaming
2. Use the safety features in brain.py
3. Monitor the history page
4. Stop the tunnel when not needed
5. Consider adding authentication for public use

---

## Cost Comparison

### LocalTunnel
- **Free**: Unlimited
- **No paid tier**: Always free

### ngrok
- **Free**: 1 process, 2-hour sessions, warning page
- **Personal ($8/mo)**: 3 processes, no time limit, no warning
- **Pro ($20/mo)**: Custom domains, more connections
- **Business ($40+/mo)**: IP whitelisting, SSO

### Cloudflare
- **Free**: Unlimited, no time limits
- **Paid**: Enterprise features ($200+/mo)

---

## Final Recommendation

**Just want it to work?**
→ `python start_with_bore.py` (Best option!)

**Bore install failing?**
→ `python start_with_ngrok.py` (Requires signup but reliable)

**Nothing else works?**
→ `python start_with_localtunnel.py` (Has password page, but free)

**Running 24/7?**
→ Setup Cloudflare Tunnel or get a VPS

---

## Support & Help

Having issues? Check:
1. The launcher output for errors
2. This guide's troubleshooting section
3. The specific tool's documentation:
   - LocalTunnel: https://theboroer.github.io/localtunnel-www/
   - ngrok: https://ngrok.com/docs
   - Cloudflare: https://developers.cloudflare.com/cloudflare-one/
