# YouTube Super Chat Listener Setup Guide

## Overview
The YouTube listener monitors your live stream chat for Super Chats (donations) and automatically sends them to the AI Chaos system.

## Features
- ✅ **Super Chat Detection** - Automatically processes paid messages
- ✅ **Minimum Amount Filter** - Only activate chaos for donations above a threshold
- ✅ **Regular Chat Support** - Optionally allow free chat commands
- ✅ **Rate Limiting** - Prevent spam with per-user cooldowns
- ✅ **Real-time Processing** - Instant chaos activation
- ✅ **Safe Integration** - Works alongside your existing stream

## Installation

### 1. Install Python Dependencies
```bash
pip install pytchat requests
```

Or install all dependencies:
```bash
pip install -r requirements.txt
```

### 2. Configure the Listener

Edit `youtube_listener.py` and set:

```python
# Your stream's video ID (from URL)
VIDEO_ID = "abc123xyz"  # Replace with your actual stream ID

# Minimum donation amount (in USD)
MIN_SUPER_CHAT_AMOUNT = 1.00  # $1 minimum

# Optional: Allow free chat commands
ALLOW_REGULAR_CHAT = False  # Set to True to enable
CHAT_COMMAND = "!chaos"     # Command prefix if enabled
```

### 3. Get Your Video ID

#### During Live Stream:
1. Go to your YouTube live stream
2. Copy the URL: `youtube.com/watch?v=VIDEO_ID`
3. Extract the `VIDEO_ID` part
4. Example: `youtube.com/watch?v=dQw4w9WgXcQ` → Video ID is `dQw4w9WgXcQ`

#### From YouTube Studio:
1. Open YouTube Studio
2. Go to "Content" → Click your live stream
3. The video ID is in the URL or video details

## Usage

### 1. Start the Brain (if not already running)
```bash
python brain.py
```

### 2. Start the YouTube Listener
```bash
python youtube_listener.py
```

You should see:
```
============================================================
YouTube AI Chaos Listener
============================================================
Video ID: your_video_id
Brain URL: http://127.0.0.1:5000/trigger
Min Super Chat: $1.0
Regular Chat Enabled: False
============================================================
Connecting to YouTube Live Chat...

✓ Connected! Listening for Super Chats...
```

### 3. Test with a Super Chat
- Have someone send a Super Chat on your stream
- The message content becomes the chaos command
- Example Super Chat: "Make everyone tiny"
- The listener will automatically send it to the AI

## Configuration Options

### Minimum Super Chat Amount
```python
MIN_SUPER_CHAT_AMOUNT = 5.00  # Require $5 minimum
```
Only Super Chats worth $5+ will trigger chaos.

### Enable Regular Chat Commands
```python
ALLOW_REGULAR_CHAT = True
CHAT_COMMAND = "!chaos"
```
Users can type `!chaos make everyone tiny` without donating.

### Cooldown Settings
```python
COOLDOWN_SECONDS = 10  # 10 seconds between commands per user
```
Prevents spam from the same user.

## How It Works

1. **Listener connects** to your YouTube live stream chat
2. **Monitors all messages** in real-time
3. **Detects Super Chats** and extracts amount/currency
4. **Validates amount** against minimum threshold
5. **Checks cooldown** to prevent spam
6. **Sends to brain** at `http://127.0.0.1:5000/trigger`
7. **AI generates code** and executes in GMod

## Example Output

```
[14:32:15] 💰 SUPER CHAT from UserName: $5.00 USD
           Message: Spawn 10 headcrabs
           ✓ Chaos activated!

[14:32:45] 💰 SUPER CHAT from AnotherUser: $2.00 USD
           Message: Make the screen rainbow
           ✗ Amount too low (min: $5.0)

[14:33:10] 💬 Regular chat from CoolViewer: !chaos make everyone jump
           ✓ Chaos activated!
```

## Multiple Currency Support

The listener automatically handles different currencies:
- USD ($5.00)
- EUR (€5.00)
- GBP (£5.00)
- JPY (¥500)
- And more!

The amount is converted to a numeric value for comparison.

## Safety Features

1. **Amount Validation** - Ensures minimum donation threshold
2. **Rate Limiting** - Per-user cooldowns prevent spam
3. **Brain Safety Checks** - All prompts go through the brain's safety filters
4. **Error Handling** - Gracefully handles connection issues

## Troubleshooting

### "Invalid video ID" Error
- Make sure your stream is **live** (not scheduled or ended)
- Double-check the video ID from your stream URL
- Video ID should be 11 characters (letters, numbers, underscores, hyphens)

### "Chat has ended" Message
- Stream must be actively live
- Chat must be enabled on the stream
- Try restarting the listener after stream starts

### "Failed to connect to brain"
- Make sure `brain.py` is running on port 5000
- Check that `BRAIN_URL = "http://127.0.0.1:5000/trigger"`
- Test the brain directly at `http://127.0.0.1:5000/`

### Super Chats Not Detected
- Verify `pytchat` is installed: `pip install pytchat`
- Make sure Super Chats are enabled on your channel
- Check YouTube's monetization requirements

### Module Not Found: pytchat
```bash
pip install pytchat
```

## Running Both Twitch and YouTube

You can run multiple listeners simultaneously:

**Terminal 1:**
```bash
python brain.py
```

**Terminal 2:**
```bash
python twitch_listener.py
```

**Terminal 3:**
```bash
python youtube_listener.py
```

Both platforms will send commands to the same brain!

## Tips for Streamers

1. **Set appropriate minimums** - Don't make it too cheap or too expensive
2. **Test before going live** - Use a test stream to verify it works
3. **Have the history page open** - Monitor on a second screen (`/history`)
4. **Use Force Undo** - For stubborn effects that won't stop
5. **Communicate with viewers** - Let them know the format and rules

## Viewer Instructions to Share

*"Send a Super Chat with your chaos command as the message! Minimum $1. Examples:*
- *"Make everyone tiny"*
- *"Spawn 5 zombies in front of the player"*
- *"Turn the screen upside down for 10 seconds"*

*The AI will generate the code and execute it live!"*

## Advanced: Custom Filtering

You can add custom filters before sending to brain:

```python
def send_to_brain(prompt, author, amount=None):
    # Custom filter
    if "banned_word" in prompt.lower():
        print(f"           ✗ Blocked: Contains banned word")
        return False
    
    # VIP bypass cooldown
    if author in ["VIPUser1", "VIPUser2"]:
        # Allow VIPs to bypass cooldown
        pass
    
    # Original code continues...
```

## Legal & Ethical Notes

- Respect YouTube's Terms of Service
- Don't encourage dangerous or harmful donations
- Have clear rules for what's allowed
- Consider refund policies for blocked commands
- Moderate appropriately for your audience

## Support

If you encounter issues:
1. Check the console output for error messages
2. Verify all configuration values
3. Test the brain independently first
4. Ensure stream is live before starting listener
