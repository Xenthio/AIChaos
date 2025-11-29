#!/usr/bin/env python3
"""
AI Chaos Launcher with ngrok
Automatically starts ngrok, gets the public URL, and starts brain.py
"""

import subprocess
import time
import requests
import os
import sys
import signal

NGROK_PORT = 5000
SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
CONFIG_FILE = os.path.join(SCRIPT_DIR, "ngrok_url.txt")
LUA_FILE = os.path.join(SCRIPT_DIR, "lua", "autorun", "ai_chaos_controller.lua")

ngrok_process = None
brain_process = None

def cleanup():
    """Stop ngrok and brain processes"""
    global ngrok_process, brain_process
    
    print("\n\nShutting down...")
    
    if brain_process:
        print("Stopping brain.py...")
        brain_process.terminate()
        try:
            brain_process.wait(timeout=5)
        except:
            brain_process.kill()
    
    if ngrok_process:
        print("Stopping ngrok...")
        ngrok_process.terminate()
        try:
            ngrok_process.wait(timeout=5)
        except:
            ngrok_process.kill()
    
    print("Done!")

def signal_handler(sig, frame):
    """Handle Ctrl+C gracefully"""
    cleanup()
    sys.exit(0)

def check_ngrok():
    """Check if ngrok is installed"""
    try:
        result = subprocess.run(['ngrok', 'version'], 
                              capture_output=True, 
                              text=True,
                              timeout=5)
        return result.returncode == 0
    except:
        return False

def start_ngrok():
    """Start ngrok tunnel"""
    global ngrok_process
    
    print(f"[1/5] Starting ngrok tunnel on port {NGROK_PORT}...")
    
    # Start ngrok in background
    ngrok_process = subprocess.Popen(
        ['ngrok', 'http', str(NGROK_PORT)],
        stdout=subprocess.DEVNULL,
        stderr=subprocess.DEVNULL
    )
    
    print("   ngrok started (PID: {})".format(ngrok_process.pid))
    
    # Wait for ngrok to initialize
    print("   Waiting for ngrok to initialize...")
    time.sleep(3)
    
    return True

def get_ngrok_url():
    """Get the public ngrok URL"""
    print("[2/5] Fetching ngrok URL from API...")
    
    max_retries = 10
    for attempt in range(max_retries):
        try:
            response = requests.get('http://localhost:4040/api/tunnels', timeout=2)
            if response.status_code == 200:
                data = response.json()
                if data.get('tunnels') and len(data['tunnels']) > 0:
                    url = data['tunnels'][0]['public_url']
                    # Force HTTPS
                    url = url.replace('http://', 'https://')
                    print(f"   Tunnel URL: {url}")
                    return url
        except Exception as e:
            if attempt < max_retries - 1:
                print(f"   Waiting for ngrok API... (Attempt {attempt + 1}/{max_retries})")
                time.sleep(1)
            else:
                print(f"   Error: {e}")
    
    return None

def save_config(url):
    """Save ngrok URL to config file"""
    print("[3/5] Saving URL to config file...")
    
    try:
        with open(CONFIG_FILE, 'w') as f:
            f.write(url)
        print(f"   Saved to: ngrok_url.txt")
        return True
    except Exception as e:
        print(f"   Error saving config: {e}")
        return False

def update_lua_file(url):
    """Update Lua file with the ngrok URL"""
    print("[4/5] Updating Lua configuration...")
    
    if not os.path.exists(LUA_FILE):
        print(f"   WARNING: Lua file not found at {LUA_FILE}")
        print(f"   Manually set SERVER_URL to: {url}/poll")
        return False
    
    try:
        with open(LUA_FILE, 'r', encoding='utf-8') as f:
            content = f.read()
        
        # Replace the SERVER_URL line
        import re
        poll_url = f"{url}/poll"
        new_line = f'    local SERVER_URL = "{poll_url}" -- Auto-configured by launcher'
        
        content = re.sub(
            r'local SERVER_URL = ".*?".*',
            new_line,
            content
        )
        
        with open(LUA_FILE, 'w', encoding='utf-8') as f:
            f.write(content)
        
        print(f"   Updated: ai_chaos_controller.lua")
        print(f"   Poll URL: {poll_url}")
        return True
        
    except Exception as e:
        print(f"   Error updating Lua file: {e}")
        return False

def start_brain():
    """Start brain.py"""
    global brain_process
    
    print("[5/5] Starting brain.py...")
    print("")
    print("=" * 60)
    print("Brain Starting - Press Ctrl+C to stop everything")
    print("=" * 60)
    print("")
    
    brain_process = subprocess.Popen(
        [sys.executable, 'brain.py'],
        cwd=SCRIPT_DIR
    )
    
    return brain_process

def main():
    """Main launcher function"""
    # Register signal handler for Ctrl+C
    signal.signal(signal.SIGINT, signal_handler)
    
    print("=" * 60)
    print("AI Chaos Launcher with ngrok")
    print("=" * 60)
    print("")
    
    # Check ngrok
    if not check_ngrok():
        print("ERROR: ngrok is not installed or not in PATH!")
        print("")
        print("Please install ngrok:")
        print("1. Download from: https://ngrok.com/download")
        print("2. Extract and add to PATH")
        print("")
        print("Or install via package manager:")
        print("  Windows: winget install ngrok")
        print("  macOS:   brew install ngrok")
        print("  Linux:   snap install ngrok")
        print("")
        return 1
    
    # Start ngrok
    if not start_ngrok():
        print("Failed to start ngrok!")
        return 1
    
    # Get URL
    url = get_ngrok_url()
    if not url:
        print("ERROR: Could not get ngrok URL!")
        print("Make sure ngrok is running at http://localhost:4040")
        cleanup()
        return 1
    
    print("")
    
    # Save config
    save_config(url)
    print("")
    
    # Update Lua
    update_lua_file(url)
    print("")
    
    # Display info
    print("=" * 60)
    print("ngrok Configuration Complete!")
    print("=" * 60)
    print("")
    print(f"Public URL:  {url}")
    print(f"Web UI:      {url}/")
    print(f"History:     {url}/history")
    print("")
    print("Next: Restart Garry's Mod to load the updated Lua script")
    print("")
    
    # Start brain
    brain = start_brain()
    
    # Wait for brain to finish
    try:
        brain.wait()
    except KeyboardInterrupt:
        pass
    
    cleanup()
    return 0

if __name__ == "__main__":
    try:
        sys.exit(main())
    except Exception as e:
        print(f"\nUnexpected error: {e}")
        cleanup()
        sys.exit(1)
