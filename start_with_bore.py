#!/usr/bin/env python3
"""
AI Chaos Launcher with Bore (bore.pub)
Free tunnel service - No account, no password pages!
"""

import subprocess
import time
import requests
import os
import sys
import signal
import re
import threading
import queue

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
        print("Stopping Bore tunnel...")
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

def check_bore():
    """Check if bore is installed"""
    try:
        result = subprocess.run(['bore', '--version'], 
                              capture_output=True, 
                              text=True,
                              timeout=5)
        return result.returncode == 0
    except:
        return False

def install_bore():
    """Try to install bore via cargo (Rust package manager)"""
    print("\nBore is not installed. Attempting to install...")
    print("This requires Rust/Cargo to be installed.\n")
    
    try:
        # Check if cargo exists
        result = subprocess.run(['cargo', '--version'], 
                              capture_output=True,
                              timeout=5)
        if result.returncode != 0:
            print("ERROR: Cargo (Rust) is not installed!")
            print("\nPlease install Rust from: https://rustup.rs/")
            print("Or download bore binary from: https://github.com/ekzhang/bore/releases")
            return False
        
        print("Installing bore via cargo...")
        print("(This may take a few minutes on first install)")
        result = subprocess.run(['cargo', 'install', 'bore-cli'],
                              capture_output=True,
                              text=True)
        
        if result.returncode == 0:
            print("✓ Bore installed successfully!")
            return True
        else:
            print(f"Failed to install: {result.stderr}")
            return False
            
    except Exception as e:
        print(f"Installation error: {e}")
        return False

def start_bore():
    """Start Bore tunnel"""
    global tunnel_process
    
    print(f"[1/5] Starting Bore tunnel on port {BRAIN_PORT}...")
    
    # Bore command: bore local <local_port> --to bore.pub
    # Use unbuffered output
    tunnel_process = subprocess.Popen(
        ['bore', 'local', str(BRAIN_PORT), '--to', 'bore.pub'],
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        text=True,
        bufsize=0  # Unbuffered
    )
    
    print("   Bore tunnel started")
    return True

def read_stream(stream, output_queue):
    """Thread function to read from a stream and put lines in queue"""
    try:
        for line in iter(stream.readline, ''):
            if line:
                output_queue.put(line)
    except:
        pass
    stream.close()

def get_tunnel_url():
    """Get the public tunnel URL from Bore output"""
    print("[2/5] Waiting for tunnel URL...")
    
    url = None
    timeout = 30
    start_time = time.time()
    
    # Create queue and thread to read stderr asynchronously
    output_queue = queue.Queue()
    stderr_thread = threading.Thread(target=read_stream, args=(tunnel_process.stderr, output_queue))
    stderr_thread.daemon = True
    stderr_thread.start()
    
    while time.time() - start_time < timeout:
        if tunnel_process.poll() is not None:
            # Process died
            print(f"   ERROR: Bore exited unexpectedly")
            return None
        
        # Try to get output from queue (non-blocking)
        try:
            line = output_queue.get(timeout=0.1)
            line = line.strip()
            
            # Look for URL pattern in output
            # Bore outputs something like: "listening at bore.pub:12345"
            if 'bore.pub' in line.lower():
                # Extract port number
                match = re.search(r'bore\.pub:(\d+)', line)
                if match:
                    port = match.group(1)
                    url = f"http://bore.pub:{port}"
                    print(f"   ✓ Found tunnel URL: {url}")
                    return url
        except queue.Empty:
            continue
    
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
    print("AI Chaos Launcher with Bore")
    print("(No account, no password page!)")
    print("=" * 60)
    print("")
    
    # Check Bore
    if not check_bore():
        print("Bore is not installed.")
        print("")
        
        # Ask if user wants to install
        print("Installation options:")
        print("1. Auto-install via Cargo (requires Rust)")
        print("2. Manual download from GitHub")
        print("")
        
        choice = input("Try auto-install? (y/n): ").lower().strip()
        
        if choice == 'y':
            if not install_bore():
                print("\nManual installation:")
                print("  Option A - Install Rust, then:")
                print("    cargo install bore-cli")
                print("")
                print("  Option B - Download binary:")
                print("    https://github.com/ekzhang/bore/releases")
                print("    Extract and add to PATH")
                print("")
                return 1
            
            # Verify installation
            if not check_bore():
                print("\nInstallation appeared to succeed but 'bore' command not found.")
                print("You may need to restart your terminal or add cargo bin to PATH")
                return 1
        else:
            print("\nManual installation:")
            print("  1. Install Rust: https://rustup.rs/")
            print("  2. Run: cargo install bore-cli")
            print("  OR download: https://github.com/ekzhang/bore/releases")
            return 1
    
    print("✓ Bore is installed\n")
    
    # Start tunnel
    if not start_bore():
        print("Failed to start Bore!")
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
    print("Bore Tunnel Configuration Complete!")
    print("=" * 60)
    print("")
    print(f"Public URL:  {url}")
    print(f"Web UI:      {url}/")
    print(f"History:     {url}/history")
    print("")
    print("✓ No account required!")
    print("✓ No password page!")
    print("✓ Direct access for everyone!")
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
