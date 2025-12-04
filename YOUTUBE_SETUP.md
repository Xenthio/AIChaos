# YouTube Super Chat Integration - StreamReady Guide

## Overview
The YouTube integration for Chaos enables a streamlined "invisible economy" where viewers donate via Super Chat to send Ideas to your game. The system uses a $1 per Idea pricing model with hidden balances and real-time account linking.

## Features
- ✅ **$1 per Idea** - Simple, transparent pricing
- ✅ **Invisible Economy** - Balances hidden from main UI
- ✅ **Pending Credits** - Credits auto-transfer when viewers link accounts
- ✅ **Real-time Linking** - Viewers type codes in chat to link their YouTube channel
- ✅ **Slot-based Queue** - Dynamic pacing (3-10 concurrent commands based on demand)
- ✅ **Unified Dashboard** - Stream Control panel for all stream management
- ✅ **Role-based Access** - Moderator and Admin permissions

## Quick Setup

### Option 1: Stream Control (Recommended)

1. Start the Brain server: `cd AIChaos.Brain && dotnet run`
2. Open **http://localhost:5000/dashboard**
3. Go to **Stream Control** tab (default landing page)
4. Click **"🔗 Login with YouTube"**
5. Authorize the app with your YouTube account
6. Enter your live stream's **Video ID**
7. Click **"Save Video ID"** (automatically starts listening)

✅ **Done!** The system is now listening for Super Chats and link codes.

> **⚠️ Important:** You MUST login with YouTube (step 4) for the app to access live chat. Without OAuth authorization, the system cannot see chat messages or Super Chats.

### Option 2: Full OAuth Setup

If you need to configure OAuth credentials first:

1. Go to [Google Cloud Console](https://console.cloud.google.com/)
2. Create a new project
3. Enable **YouTube Data API v3**
4. **Configure OAuth Consent Screen:**
   - Choose **"External"** as User Type (allows anyone to login)
   - Fill in required app information
   - Add your email to test users (optional but recommended)
   - Publishing status: Keep in "Testing" mode initially
5. Create OAuth 2.0 credentials (Web Application)
6. Add redirect URI: `http://localhost:5000/api/setup/youtube/callback`
7. Copy Client ID and Client Secret
8. Go to Dashboard → **Setup** tab
9. Enter credentials in YouTube Integration section
10. Click "Login with YouTube"
11. Return to Stream Control to enter Video ID

> **📝 Note:** Setting User Type to "External" allows up to 100 people to login with OAuth before verification. This is perfect for letting viewers authenticate directly!

### ⚠️ Google OAuth User Cap (100 Users for Unverified Apps)

**Important Limitation:**
- Unverified Google Cloud apps with "External" user type are limited to **100 OAuth logins**
- After 100 users authenticate via OAuth, additional users cannot login
- **Solution:** Chaos offers TWO ways to link accounts

**Method 1: OAuth Login (First 100 Users)**
1. Set OAuth consent screen to **"External"** (see setup above)
2. First 100 viewers can click "Login with Google" on your site
3. They authenticate directly with their YouTube account
4. Instant account linking - no chat codes needed
5. Easiest experience for early adopters

**Method 2: Link Codes (Unlimited - Backup After 100)**
1. Viewer goes to your public URL
2. Gets unique code (e.g., `LINK-4B2R`)
3. Types code in YouTube chat
4. System links their channel WITHOUT OAuth
5. No impact on 100 user limit - works for users 101+

**Recommended Strategy:**
- ✅ **First 100 viewers**: Use OAuth login (easiest)
- ✅ **After 100**: Automatically switch messaging to link codes
- ✅ **Streamer/moderators**: Always use OAuth (count toward 100)
- ✅ **Link codes**: Always available as backup method

**If You Hit the 100 User Cap:**
- Viewers 1-100: Already authenticated via OAuth ✓
- Viewers 101+: Use link codes (unlimited)
- Both methods work identically after linking
- To remove cap entirely: Submit app for Google verification

**Google Verification Process:**
- [Submit for verification](https://support.google.com/cloud/answer/9110914)
- Typically takes 2-4 weeks
- Removes 100 user limit completely
- Allows unlimited OAuth logins
- Recommended if you expect 100+ concurrent viewers

### 🔧 Viewer OAuth Login Control

**Streamers can choose how viewers link their accounts:**

In the Dashboard → **Setup** → **YouTube Integration**, you'll find a checkbox:

**☑️ "Allow viewers to link via Google Sign-In (first 100 users)"**

- **Enabled (Default):** Viewers see both OAuth and link code options
  - First 100 can use "Login with Google" (fastest method)
  - Link codes always available as backup
  - Best user experience for early adopters

- **Disabled:** Viewers ONLY see link code option
  - Forces all viewers to use chat verification codes
  - Useful if you want to avoid managing OAuth user cap
  - Simpler if you don't want viewers doing OAuth flow
  - Still allows unlimited viewers via link codes

**When to disable viewer OAuth:**
- You don't want to deal with the 100 user OAuth limit
- You prefer all viewers use the same linking method
- You want to keep OAuth exclusively for streamer/moderators
- You haven't set up OAuth consent screen yet

**Note:** Disabling this setting does NOT affect:
- Streamer's ability to login with YouTube (still required)
- Link code functionality (always works)
- Pending credits system (still transfers on link)

## How It Works

### For Streamers

**Setup:**
1. Configure YouTube OAuth (one-time)
2. Enter video ID when you go live
3. System automatically listens for Super Chats and chat codes

**Stream Control Dashboard:**
- **Queue Control** (Admin only): Manual blast, queue status, active slots
- **Stream Settings** (Admin only): Video ID, OAuth login
- **Incoming Links**: Review all URLs before processing (moderators can access)
- **Refund Requests**: Quick approve/deny interface
- **Global History**: Recent commands with undo/save actions

### For Viewers

**Account Linking Options:**

**Option A: OAuth Login (Easiest - First 100 Users)**
1. Go to your public URL (e.g., `https://your-ngrok-url.ngrok.io`)
2. Click **"Login with Google"** button
3. Authorize with YouTube account
4. ✅ Done! Account instantly linked

**Option B: Link Code (Unlimited - Always Available)**
1. Go to your public URL (e.g., `https://your-ngrok-url.ngrok.io`)
2. Click username dropdown → **"Get a Link Code"**
3. Copy the code (e.g., `LINK-4B2R`)
4. Type the code in YouTube chat
5. System automatically links their channel and shows success message

> **💡 Tip:** OAuth is faster for early viewers, but link codes work for everyone and never run out!

**Sending Ideas:**
1. Donate $1 via Super Chat with Idea as message
   - Example: Super Chat $5 with message "Make everyone tiny"
   - Credits added: 5 (one per dollar)
2. Go to public URL and click **"Send Chaos"**
3. Type Idea and submit
4. Balance decrements by $1 per Idea (hidden from viewer)
5. When balance is $0, button shows: "You'll need to donate again to send another"

**Checking Balance:**
- Click username → View profile modal
- Balance shown only there (invisible economy)

## Pricing & Economy

### Viewer Experience

**Main Interface (Public):**
- Button: "Send Chaos" (no price shown)
- Explainer: "Each dollar gets you one Idea"
- When insufficient funds: "You'll need to donate again to send another"
- No visible wallet balance

**Profile Modal:**
- Balance visible when user clicks their username
- Maintains transparency while de-emphasizing transaction

### Pending Credits System

**How it works:**
1. Viewer donates $5 via Super Chat (channel not yet linked)
2. System stores 5 pending credits for that YouTube channel
3. Viewer later types link code in chat
4. System automatically transfers all 5 credits to their account
5. Credits immediately available for use

**Benefits:**
- Viewers can donate before creating account
- No credits lost if they link later
- Automatic transfer on first login

## Queue System

### Slot-Based Pacing

Unlike traditional FIFO queues, Chaos uses concurrent execution slots:

**Dynamic Scaling:**
- **0-5 Ideas in queue**: 3 active slots → "Drip feed" pacing
- **6-20 Ideas**: 4-6 slots → Moderate chaos
- **50+ Ideas**: 10 slots → "Absolute chaos" mode

**Slot Timer:**
- Each slot blocks for 25 seconds after execution
- Independent of actual effect duration
- Prevents overwhelming the game

**Manual Blast (Admin only):**
- Bypass slot limits entirely
- Execute 1-10 commands instantly
- Useful for clearing backlog or special events

## Stream Control Panel

The unified Stream Control tab provides all essential streaming tools:

### For Admins

**Queue Control Panel:**
- Queue depth display
- Active/total slots status
- Manual blast button (1-10 commands)
- Bypass all rate limiting

**Stream Settings Panel:**
- YouTube video ID input with auto-start
- "Login with YouTube" OAuth button
- Quick access to stream configuration

### For Moderators

**Incoming Links Panel:**
- Review all URLs from Ideas
- Approve or deny before processing
- Auto-refresh toggle (5s intervals)

**Refund Requests Panel:**
- Quick approve/deny interface
- Amount display
- Auto-refresh toggle

**Global History Panel:**
- Last 20 commands (spoiler-protected dropdown)
- Action buttons: Undo, Force Undo, Save Payload
- Auto-refresh toggle

## Getting Your Video ID

### Method 1: From URL (Easiest)

When your stream is live:
1. Go to your YouTube live stream
2. Copy the URL: `youtube.com/watch?v=VIDEO_ID_HERE`
3. Extract the `VIDEO_ID_HERE` part
4. Example: `youtube.com/watch?v=dQw4w9WgXcQ` → Video ID is `dQw4w9WgXcQ`

### Method 2: From YouTube Studio

1. Open YouTube Studio
2. Go to "Content"
3. Click your live stream
4. Video ID is in the URL or video details

### Method 3: From Live Control Room

1. Start your stream
2. Go to Live Control Room
3. Video ID is in the browser URL

## Troubleshooting

### "Invalid video ID" Error
- Stream must be **actively live** (not scheduled or ended)
- Video ID should be 11 characters
- Try copying directly from live stream URL

### "Unauthorized" Error
- **Most common cause:** OAuth not completed - click "Login with YouTube" in Setup or Stream Control
- OAuth token expired - click "Login with YouTube" again
- Verify OAuth credentials in Google Cloud Console
- Check redirect URI: `http://localhost:5000/api/setup/youtube/callback`
- **Remember:** The system CANNOT see live chat without OAuth authorization

### Link Codes Not Working
- **First check:** Is OAuth authorized? Click "Login with YouTube" if not
- **Second check:** Is the chat listener running? Look for "✓ Video ID saved and listening to chat" message
- Check server logs for `[YouTube]` and `[ACCOUNT]` messages
- Verify chat listener is running (see "✓ Connected!" message)
- Make sure viewer types exact code (case-sensitive)
- Codes expire after 30 minutes
- Remember: Link codes bypass the 100 user OAuth limit!

### Pending Credits Not Transferring
- Check `pending_credits.json` file exists
- Verify viewer is typing correct link code
- Check server logs for transfer confirmation
- Credits transfer is automatic when channel links

### Super Chats Not Processing
- Verify minimum amount is set correctly ($1 default)
- Check YouTube monetization is enabled
- Super Chat feature must be enabled on channel
- Ensure OAuth is authenticated

## Configuration Options

### Minimum Super Chat Amount

In Dashboard → Setup:
```
Minimum Super Chat Amount: $1.00
```
Only Super Chats worth $1+ will add credits.

### Slot Timing

In `QueueSlotService.cs`:
```csharp
private const int SlotTimerSeconds = 25; // Default: 25 seconds
```
Adjust pacing by changing slot timer duration.

### Queue Slot Scaling

In `QueueSlotService.cs`:
```csharp
private int DetermineSlotCount(int queueDepth)
{
    if (queueDepth <= 5) return 3;    // Low volume
    if (queueDepth <= 10) return 4;   // Building up
    if (queueDepth <= 20) return 6;   // Moderate chaos
    if (queueDepth <= 50) return 8;   // High activity
    return 10;                         // Absolute chaos
}
```

## Viewer Communication

### Suggested Stream Overlay Text

```
💰 Send Ideas via Super Chat! 💰
$1 = 1 Idea

How to participate:
1. Go to [YOUR_URL]
2. Link your account:
   → Click "Login with Google" (fastest!)
   → OR get a link code and type in chat
3. Super Chat $1+ to get credits
4. Submit your Ideas!

Examples:
• "Make everyone tiny"
• "Spawn 5 headcrabs"
• "Rainbow screen for 10 seconds"
```

### Chat Commands to Share

```
!link - Get instructions to link your YouTube channel
!balance - Check your Idea credit balance (via website)
!ideas - See example Ideas to try
```

## Security & Moderation

### Link Review (All Moderators)

**Incoming Links panel:**
- All URLs extracted from Ideas
- Review before approval
- Prevent malicious links

### Refund Management

**When to approve refunds:**
- Technical issues prevented execution
- Inappropriate content (violates rules)
- Duplicate charges

**When to deny:**
- Idea executed successfully
- User changed mind after execution
- Violates refund policy

## Advanced: Multiple Streams

You can run multiple YouTube listeners:

**Terminal 1 (Brain):**
```bash
cd AIChaos.Brain
dotnet run
```

**Stream Control:**
- Enter Video ID for current stream
- Click "Save Video ID" (auto-starts listener)
- When switching streams, enter new Video ID

The system supports hot-swapping between streams without restart.

## Best Practices

1. **Test before going live:**
   - Use a test stream to verify OAuth
   - Test link codes with a secondary account
   - Verify Super Chat processing

2. **Have Dashboard open:**
   - Monitor Stream Control on second screen
   - Watch queue depth and slot status
   - Use manual blast for special moments

3. **Set clear rules:**
   - Communicate $1 per Idea pricing
   - Explain link code process
   - Share example Ideas

4. **Moderate appropriately:**
   - Review incoming links
   - Handle refund requests promptly
   - Use Force Undo for problematic effects

5. **Communicate with viewers:**
   - Acknowledge donations in real-time
   - Explain when Ideas are processing
   - Thank viewers for participation

## Support

For issues:
1. Check server console for error messages
2. Review Dashboard → History tab for failures
3. Verify OAuth credentials in Setup
4. Test with small Super Chat first
5. Check [GitHub Issues](https://github.com/Xenthio/AIChaos/issues)

## License

See the main repository for license information.
