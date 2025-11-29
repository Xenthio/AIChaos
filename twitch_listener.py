"""
Twitch Chat & Bits/Subscriptions Listener for AI Chaos
Monitors Twitch chat for commands, bits, and subscriptions
"""

from twitchio.ext import commands
import requests
import time
import re
from urllib.parse import urlparse
from datetime import datetime

# ==========================================
# TWITCH CONFIGURATION
# ==========================================

TOKEN = 'oauth:your_token_here'  # Get from https://twitchtokengenerator.com/
CHANNEL = 'your_channel_name'
BRAIN_URL = "http://127.0.0.1:5000/trigger"

# Command Settings
CHAT_COMMAND = '!chaos'           # Command prefix for chaos
REQUIRE_BITS = False              # Require bits/donations to use chaos
MIN_BITS_AMOUNT = 100             # Minimum bits required (if REQUIRE_BITS is True)

# URL Filtering
BLOCK_URLS = True                 # Block URLs in messages (except from moderators)
MODERATORS = []                   # Additional moderator usernames who can send URLs
                                  # Example: ["ModName1", "ModName2"]
ALLOWED_DOMAINS = [               # Domains that are always allowed
    "i.imgur.com",
    "imgur.com"
]

# Rate Limiting
COOLDOWN_SECONDS = 5              # Minimum seconds between commands from same user
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
    url_pattern = r'http[s]?://(?:[a-zA-Z]|[0-9]|[$-_@.&+]|[!*\\(\\),]|(?:%[0-9a-fA-F][0-9a-fA-F]))+'
    return re.findall(url_pattern, text)

def is_moderator(ctx):
    """Check if user is a moderator"""
    # Check if user has mod badge or is broadcaster
    is_mod = ctx.author.is_mod or ctx.author.is_broadcaster
    # Also check manual moderator list
    return is_mod or ctx.author.name in MODERATORS

def filter_urls(message, ctx):
    """Filter URLs from message unless from moderator or whitelisted"""
    if not BLOCK_URLS:
        return message, False
    
    # Moderators can send any URLs
    if is_moderator(ctx):
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

def send_to_brain(prompt, author, bits=None):
    """Send chaos command to the brain"""
    try:
        response = requests.post(BRAIN_URL, json={"prompt": prompt}, timeout=5)
        
        if response.status_code == 200:
            data = response.json()
            if data.get('status') == 'queued':
                bits_info = f" ({bits} bits)" if bits else ""
                print(f"✓ Command queued{bits_info}: {prompt}")
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

# ==========================================
# BOT CLASS
# ==========================================

class Bot(commands.Bot):
    def __init__(self):
        super().__init__(token=TOKEN, prefix='!', initial_channels=[CHANNEL])
        print("=" * 60)
        print("Twitch AI Chaos Listener")
        print("=" * 60)
        print(f"Channel: {CHANNEL}")
        print(f"Brain URL: {BRAIN_URL}")
        print(f"Require Bits: {REQUIRE_BITS}")
        if REQUIRE_BITS:
            print(f"Min Bits: {MIN_BITS_AMOUNT}")
        print(f"Command: {CHAT_COMMAND}")
        print(f"URL Blocking: {BLOCK_URLS}")
        print("=" * 60)

    async def event_ready(self):
        print(f'✓ Logged in as: {self.nick}')
        print(f'✓ Connected to channel: {CHANNEL}')
        print('✓ Listening for chaos commands...\n')

    async def event_message(self, message):
        # Ignore bot messages
        if message.echo:
            return
        
        # Handle commands
        await self.handle_commands(message)

    @commands.command(name='chaos')
    async def chaos(self, ctx: commands.Context, *, prompt: str = None):
        """Main chaos command handler"""
        timestamp = datetime.now().strftime('%H:%M:%S')
        author = ctx.author.name
        
        if not prompt:
            await ctx.send(f"@{author} Usage: {CHAT_COMMAND} <your chaos request>")
            return
        
        # Check for bits in the message
        bits = 0
        if hasattr(ctx.message, 'tags') and ctx.message.tags:
            bits = int(ctx.message.tags.get('bits', 0))
        
        # Log the request
        if bits > 0:
            print(f"[{timestamp}] 💎 BITS from {author}: {bits} bits")
        else:
            print(f"[{timestamp}] 💬 Chat from {author}")
        print(f"           Message: {prompt}")
        
        # Check if bits are required
        if REQUIRE_BITS and bits < MIN_BITS_AMOUNT:
            print(f"           ✗ Insufficient bits (min: {MIN_BITS_AMOUNT})")
            await ctx.send(f"@{author} Please cheer with at least {MIN_BITS_AMOUNT} bits to use chaos!")
            return
        
        # Check cooldown
        if is_on_cooldown(author):
            print(f"           ⏱ User on cooldown, skipping...")
            await ctx.send(f"@{author} Please wait {COOLDOWN_SECONDS} seconds between commands!")
            return
        
        # Filter URLs
        filtered_prompt, was_filtered = filter_urls(prompt, ctx)
        
        if was_filtered:
            print(f"           🚫 URLs removed (user is not a moderator)")
            print(f"           Filtered: {filtered_prompt}")
            await ctx.send(f"@{author} URLs have been filtered from your message.")
        
        # Send to brain
        await ctx.send(f"@{author} Processing your chaos request...")
        
        if send_to_brain(filtered_prompt, author, bits if bits > 0 else None):
            set_cooldown(author)
            print(f"           ✓ Chaos activated!")
            await ctx.send(f"@{author} Chaos has been unleashed! 🎮")
        else:
            print(f"           ✗ Failed to process")
            await ctx.send(f"@{author} Failed to process your request. The AI Brain might be offline!")
        
        print()

# ==========================================
# ENTRY POINT
# ==========================================

if __name__ == "__main__":
    # Validate configuration
    if TOKEN == 'oauth:your_token_here' or CHANNEL == 'your_channel_name':
        print("=" * 60)
        print("ERROR: Please configure your Twitch settings first!")
        print("=" * 60)
        print()
        print("Instructions:")
        print("1. Get your OAuth token from: https://twitchtokengenerator.com/")
        print("2. Set TOKEN = 'oauth:your_token_here'")
        print("3. Set CHANNEL = 'your_channel_name'")
        print()
        print("For bits detection, make sure you have:")
        print("- Channel Points Predictions enabled")
        print("- Appropriate bot permissions")
        print("=" * 60)
    else:
        bot = Bot()
        bot.run()