"""
YouTube Live Chat & Super Chat Listener for AI Chaos
Monitors YouTube live chat and processes Super Chats as AI chaos commands
"""

import time
import requests
import pytchat
from datetime import datetime

# ==========================================
# CONFIGURATION
# ==========================================

# Your YouTube Video/Stream ID (from the URL: youtube.com/watch?v=VIDEO_ID)
VIDEO_ID = "YOUR_VIDEO_ID_HERE"

# Brain API endpoint
BRAIN_URL = "http://127.0.0.1:5000/trigger"

# Super Chat Settings
MIN_SUPER_CHAT_AMOUNT = 1.00  # Minimum $ amount to trigger chaos (in USD)
ALLOW_REGULAR_CHAT = False    # Allow non-donation chat messages to trigger chaos
CHAT_COMMAND = "!chaos"       # Command for regular chat (if enabled)

# URL Filtering
BLOCK_URLS = True             # Block URLs in messages (except from moderators)
MODERATORS = []               # List of moderator usernames who can send URLs
                              # Example: ["ModName1", "ModName2"]
ALLOWED_DOMAINS = [           # Domains that are always allowed
    "i.imgur.com",
    "imgur.com"
]

# Rate limiting
COOLDOWN_SECONDS = 5          # Minimum seconds between commands from same user
user_cooldowns = {}

# ==========================================
# HELPER FUNCTIONS
# ==========================================

def is_on_cooldown(author):
    """Check if user is on cooldown"""
    if author in user_cooldowns:
        elapsed = time.time() - user_cooldowns[author]
        if elapsed < COOLDOWN_SECONDS:
            return True
    return False

def set_cooldown(author):
    """Set cooldown for user"""
    user_cooldowns[author] = time.time()

def contains_url(text):
    """Check if text contains URLs"""
    import re
    url_pattern = r'http[s]?://(?:[a-zA-Z]|[0-9]|[$-_@.&+]|[!*\(\),]|(?:%[0-9a-fA-F][0-9a-fA-F]))+'
    return re.findall(url_pattern, text)

def is_moderator(author, is_mod_flag=False):
    """Check if user is a moderator"""
    # Check if author is in moderator list or has moderator flag
    return author in MODERATORS or is_mod_flag

def filter_urls(message, author, is_mod=False):
    """Filter URLs from message unless from moderator or whitelisted"""
    import re
    from urllib.parse import urlparse
    
    if not BLOCK_URLS:
        return message, False
    
    # Moderators can send any URLs
    if is_moderator(author, is_mod):
        return message, False
    
    urls = contains_url(message)
    if not urls:
        return message, False
    
    # Check if all URLs are from allowed domains
    filtered = False
    filtered_message = message
    
    for url in urls:
        try:
            parsed = urlparse(url)
            domain = parsed.netloc.lower()
            
            # Check if domain is whitelisted
            is_allowed = any(allowed in domain for allowed in ALLOWED_DOMAINS)
            
            if not is_allowed:
                # Remove the URL
                filtered_message = filtered_message.replace(url, "[URL REMOVED]")
                filtered = True
        except:
            # If parsing fails, remove the URL to be safe
            filtered_message = filtered_message.replace(url, "[URL REMOVED]")
            filtered = True
    
    return filtered_message, filtered

def send_to_brain(prompt, author, amount=None):
    """Send chaos command to the brain"""
    try:
        response = requests.post(BRAIN_URL, json={"prompt": prompt}, timeout=5)
        
        if response.status_code == 200:
            data = response.json()
            if data.get('status') == 'queued':
                donation_info = f" (${amount} Super Chat)" if amount else ""
                print(f"✓ Command queued{donation_info}: {prompt}")
                return True
            elif data.get('status') == 'ignored':
                print(f"✗ Command blocked by safety: {data.get('message')}")
                return False
        else:
            print(f"✗ Brain returned error: {response.status_code}")
            return False
            
    except requests.exceptions.RequestException as e:
        print(f"✗ Failed to connect to brain: {e}")
        return False

def format_super_chat_message(chat):
    """Extract and format Super Chat information"""
    amount = chat.amountString if hasattr(chat, 'amountString') else "Unknown"
    currency = chat.currency if hasattr(chat, 'currency') else ""
    return amount, currency

# ==========================================
# MAIN CHAT LISTENER
# ==========================================

def main():
    print("=" * 60)
    print("YouTube AI Chaos Listener")
    print("=" * 60)
    print(f"Video ID: {VIDEO_ID}")
    print(f"Brain URL: {BRAIN_URL}")
    print(f"Min Super Chat: ${MIN_SUPER_CHAT_AMOUNT}")
    print(f"Regular Chat Enabled: {ALLOW_REGULAR_CHAT}")
    if ALLOW_REGULAR_CHAT:
        print(f"Chat Command: {CHAT_COMMAND}")
    print("=" * 60)
    print("Connecting to YouTube Live Chat...")
    print()
    
    try:
        # Connect to live chat
        chat = pytchat.create(video_id=VIDEO_ID)
        
        print("✓ Connected! Listening for Super Chats...\n")
        
        while chat.is_alive():
            for c in chat.get().sync_items():
                author = c.author.name
                message = c.message
                timestamp = datetime.now().strftime('%H:%M:%S')
                
                # Check if it's a Super Chat
                if c.type == 'superChat':
                    amount_str, currency = format_super_chat_message(c)
                    
                    # Try to extract numeric amount
                    try:
                        # Remove currency symbols and parse
                        amount_value = float(''.join(filter(lambda x: x.isdigit() or x == '.', amount_str)))
                    except:
                        amount_value = 0
                    
                    print(f"[{timestamp}] 💰 SUPER CHAT from {author}: {amount_str} {currency}")
                    print(f"           Message: {message}")
                    
                    # Check minimum amount
                    if amount_value >= MIN_SUPER_CHAT_AMOUNT:
                        # Check if user is a moderator
                        is_mod = c.author.isChatModerator if hasattr(c.author, 'isChatModerator') else False
                        
                        # Filter URLs
                        filtered_message, was_filtered = filter_urls(message, author, is_mod)
                        
                        if was_filtered:
                            print(f"           🚫 URLs removed (user is not a moderator)")
                            print(f"           Filtered: {filtered_message}")
                        
                        # Check cooldown
                        if is_on_cooldown(author):
                            print(f"           ⏱ User on cooldown, skipping...")
                            continue
                        
                        # Send to brain
                        if send_to_brain(filtered_message, author, amount_str):
                            set_cooldown(author)
                            print(f"           ✓ Chaos activated!")
                        else:
                            print(f"           ✗ Failed to process")
                    else:
                        print(f"           ✗ Amount too low (min: ${MIN_SUPER_CHAT_AMOUNT})")
                    
                    print()
                
                # Handle regular chat (if enabled)
                elif ALLOW_REGULAR_CHAT and message.startswith(CHAT_COMMAND):
                    # Extract prompt after command
                    prompt = message[len(CHAT_COMMAND):].strip()
                    
                    if not prompt:
                        continue
                    
                    print(f"[{timestamp}] 💬 Regular chat from {author}: {prompt}")
                    
                    # Check if user is a moderator
                    is_mod = c.author.isChatModerator if hasattr(c.author, 'isChatModerator') else False
                    
                    # Filter URLs
                    filtered_prompt, was_filtered = filter_urls(prompt, author, is_mod)
                    
                    if was_filtered:
                        print(f"           🚫 URLs removed (user is not a moderator)")
                        print(f"           Filtered: {filtered_prompt}")
                    
                    # Check cooldown
                    if is_on_cooldown(author):
                        print(f"           ⏱ User on cooldown, skipping...")
                        continue
                    
                    # Send to brain
                    if send_to_brain(filtered_prompt, author):
                        set_cooldown(author)
                        print(f"           ✓ Chaos activated!")
                    else:
                        print(f"           ✗ Failed to process")
                    
                    print()
            
            time.sleep(1)  # Polling interval
        
        print("Stream ended or chat closed.")
        
    except pytchat.exceptions.ChatDataFinished:
        print("Chat has ended (stream finished or chat disabled).")
    except pytchat.exceptions.InvalidVideoId:
        print(f"ERROR: Invalid video ID '{VIDEO_ID}'")
        print("Make sure you're using the correct video/stream ID from the URL.")
    except KeyboardInterrupt:
        print("\nListener stopped by user.")
    except Exception as e:
        print(f"ERROR: {e}")
        import traceback
        traceback.print_exc()

# ==========================================
# ENTRY POINT
# ==========================================

if __name__ == "__main__":
    # Validate configuration
    if VIDEO_ID == "YOUR_VIDEO_ID_HERE":
        print("=" * 60)
        print("ERROR: Please configure your VIDEO_ID first!")
        print("=" * 60)
        print()
        print("Instructions:")
        print("1. Start your YouTube live stream")
        print("2. Copy the video ID from your stream URL")
        print("   Example: youtube.com/watch?v=abc123xyz")
        print("   Video ID: abc123xyz")
        print("3. Edit this file and replace VIDEO_ID with your actual video ID")
        print()
        print("You can also find the video ID in YouTube Studio -> Stream tab")
        print("=" * 60)
    else:
        main()
