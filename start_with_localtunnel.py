#!/usr/bin/env python3
"""
AI Chaos Launcher with LocalTunnel (No account required!)
LocalTunnel is a free ngrok alternative that doesn't require signup
"""

import subprocess
import time
import requests
import os
import sys
import signal
import json
import re

BRAIN_PORT = 5000
SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
CONFIG_FILE = os.path.join(SCRIPT_DIR, "tunnel_url.txt")
LUA_FILE = os.path.join(SCRIPT_DIR, "lua", "autorun", "ai_chaos_controller.lua")

tunnel_process = None
brain_process = None

def cleanup():
    """Stop tunnel and brain processes"""
    global tunnel_process, brain_process
    
    print("\n\nShutting down...")
    
    if brain_process:
        print("Stopping brain.py...")
        brain_process.terminate()
        try:
            brain_process.wait(timeout=5)
        except:
            brain_process.kill()
    
    if tunnel_process:
        print("Stopping LocalTunnel...")
        tunnel_process.terminate()
        try:
            tunnel_process.wait(timeout=5)
        except:
            tunnel_process.kill()
    
    print("Done!")

def signal_handler(sig, frame):
    """Handle Ctrl+C gracefully"""
    cleanup()
    sys.exit(0)

def check_localtunnel():
    """Check if localtunnel (lt) is installed"""
    try:
        # Try running lt --version
        result = subprocess.run(['lt', '--version'], 
                              capture_output=True, 
                              text=True,
                              timeout=5,
                              shell=True)  # Use shell on Windows to find commands
        return result.returncode == 0
    except subprocess.TimeoutExpired:
        # If it times out, it probably exists but is waiting for input
        return True
    except Exception as e:
        # Try without shell as fallback
        try:
            result = subprocess.run(['lt', '--version'],
                                  capture_output=True,
                                  text=True,
                                  timeout=5)
            return result.returncode == 0
        except:
            return False

def install_localtunnel():
    """Try to install localtunnel via npm"""
    print("\nLocalTunnel is not installed. Attempting to install...")
    print("This requires Node.js and npm to be installed.\n")
    
    try:
        # Check if npm exists
        result = subprocess.run(['npm', '--version'], 
                              capture_output=True,
                              timeout=5,
                              shell=True)  # Use shell to find npm on Windows
        if result.returncode != 0:
            print("ERROR: npm is not installed!")
            print("\nPlease install Node.js from: https://nodejs.org/")
            print("Node.js includes npm which is needed for LocalTunnel")
            return False
        
        print("Installing localtunnel globally...")
        result = subprocess.run(['npm', 'install', '-g', 'localtunnel'],
                              capture_output=True,
                              text=True,
                              shell=True)  # Use shell for npm on Windows
        
        if result.returncode == 0:
            print("✓ LocalTunnel installed successfully!")
            return True
        else:
            print(f"Failed to install: {result.stderr}")
            return False
            
    except Exception as e:
        print(f"Installation error: {e}")
        return False

def start_localtunnel():
    """Start LocalTunnel tunnel"""
    global tunnel_process
    
    print(f"[1/5] Starting LocalTunnel on port {BRAIN_PORT}...")
    
    # LocalTunnel shows a password page by default
    # The "password" is actually your public IP address
    # We can't fully bypass this without using ngrok or similar
    # However, we'll note this in the output
    
    # Use shell=True on Windows to find the lt command
    import platform
    use_shell = platform.system() == 'Windows'
    
    tunnel_process = subprocess.Popen(
        ['lt', '--port', str(BRAIN_PORT), '--print-requests'],
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        text=True,
        bufsize=1,
        shell=use_shell
    )
    
    print("   LocalTunnel started")
    return True

def get_tunnel_url():
    """Get the public tunnel URL from LocalTunnel output"""
    print("[2/5] Waiting for tunnel URL...")
    
    url = None
    timeout = 30
    start_time = time.time()
    
    while time.time() - start_time < timeout:
        if tunnel_process.poll() is not None:
            # Process died
            stderr = tunnel_process.stderr.read()
            print(f"   ERROR: LocalTunnel exited: {stderr}")
            return None
        
        # Read line from stdout
        line = tunnel_process.stdout.readline()
        if line:
            line = line.strip()
            print(f"   LocalTunnel: {line}")
            
            # Look for URL in output
            if 'https://' in line:
                # Extract URL using regex
                match = re.search(r'https://[^\s]+', line)
                if match:
                    url = match.group(0)
                    print(f"   ✓ Tunnel URL: {url}")
                    return url
        
        time.sleep(0.1)
    
    print("   Timeout waiting for URL")
    return None

def save_config(url):
    """Save tunnel URL to config file"""
    print("[3/5] Saving URL to config file...")
    
    try:
        with open(CONFIG_FILE, 'w') as f:
            f.write(url)
        print(f"   Saved to: tunnel_url.txt")
        return True
    except Exception as e:
        print(f"   Error saving config: {e}")
        return False

def get_public_ip():
    """Get the user's public IP address for LocalTunnel password"""
    try:
        response = requests.get('https://api.ipify.org', timeout=5)
        if response.status_code == 200:
            return response.text.strip()
    except:
        pass
    
    # Fallback to another service
    try:
        response = requests.get('https://icanhazip.com', timeout=5)
        if response.status_code == 200:
            return response.text.strip()
    except:
        pass
    
    return None

def update_lua_file(url):
    """Update Lua file with the tunnel URL"""
    print("[4/5] Updating Lua configuration...")
    
    if not os.path.exists(LUA_FILE):
        print(f"   WARNING: Lua file not found at {LUA_FILE}")
        print(f"   Manually set SERVER_URL to: {url}/poll")
        return False
    
    try:
        with open(LUA_FILE, 'r', encoding='utf-8') as f:
            content = f.read()
        
        # Replace the SERVER_URL line
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
    print("AI Chaos Launcher with LocalTunnel")
    print("(No account required!)")
    print("=" * 60)
    print("")
    
    # Check LocalTunnel
    if not check_localtunnel():
        print("LocalTunnel is not installed.")
        print("")
        
        # Try to install it
        if not install_localtunnel():
            print("\nManual installation:")
            print("  1. Install Node.js: https://nodejs.org/")
            print("  2. Run: npm install -g localtunnel")
            print("")
            return 1
        
        # Verify installation
        if not check_localtunnel():
            print("\nInstallation appeared to succeed but 'lt' command not found.")
            print("You may need to restart your terminal or add npm global bin to PATH")
            return 1
    
    print("✓ LocalTunnel is installed\n")
    
    # Start tunnel
    if not start_localtunnel():
        print("Failed to start LocalTunnel!")
        return 1
    
    # Get URL
    url = get_tunnel_url()
    if not url:
        print("ERROR: Could not get tunnel URL!")
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
    print("LocalTunnel Configuration Complete!")
    print("=" * 60)
    print("")
    print(f"Public URL:  {url}")
    print(f"Web UI:      {url}/")
    print(f"History:     {url}/history")
    print("")
    print("⚠️  IMPORTANT: LocalTunnel Password Page")
    print("=" * 60)
    print("LocalTunnel will show a password page on first visit.")
    print("")
    
    # Try to get and display the public IP
    public_ip = get_public_ip()
    if public_ip:
        print(f"Your tunnel password (IP address): {public_ip}")
        print("")
        print("Copy the IP above and paste it in the password field")
    else:
        print("To get your tunnel password:")
        print("1. Visit: https://api.ipify.org")
        print("2. Copy your IP address")
        print("3. Paste it in the LocalTunnel password field")
    
    print("")
    print("💡 TIP: Use ngrok instead for no password page:")
    print("   python start_with_ngrok.py")
    print("=" * 60)
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
        import traceback
        traceback.print_exc()
        cleanup()
        sys.exit(1)
