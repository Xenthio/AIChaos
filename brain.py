import time
import re
import requests
import io
import numpy as np
import easyocr
from PIL import Image
from urllib.parse import urlparse
from flask import Flask, request, jsonify, render_template_string
from openai import OpenAI
from transformers import pipeline

# ==========================================
# CONFIGURATION
# ==========================================

YOUR_SITE_URL = "https://openrouter.ai/api/v1"
ENABLE_IMAGES = False  # Set to True to enable Image Scanning/OCR/Context

# --- SAFETY CONFIGURATION ---
TRUSTED_DOMAINS = [
    "i.imgur.com", "imgur.com", 
    #"media.discordapp.net", "cdn.discordapp.com", 
    #"upload.wikimedia.org", "pbs.twimg.com",
    #"steamuserimages-a.akamaihd.net"
]

PLACEHOLDER_IMAGE = "https://i.imgur.com/tRaI8JO.jpg" 

# Block specific words in images (OCR)
BAD_WORDS_BLOCKLIST = ["badword1", "slur1", "slur2"] 

# Strictness Levels (0.0 to 1.0)
THRESHOLD_NSFW = 0.15  # Block if > 15% confident it's hardcore
THRESHOLD_SEXY = 0.50  # Block if > 50% confident it's softcore (lingerie/bikini)

# ==========================================
# WEB UI TEMPLATE
# ==========================================

HTML_TEMPLATE = """
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>GMod AI Chaos Controller</title>
    <style>
        body { background-color: #121212; color: #00ff41; font-family: 'Courier New', monospace; padding: 20px; text-align: center; }
        h1 { text-shadow: 0 0 10px #00ff41; }
        .container { max-width: 800px; margin: 0 auto; background: #1e1e1e; padding: 20px; border-radius: 10px; border: 1px solid #333; }
        .main-controls { max-width: 600px; margin: 0 auto; }
        textarea { width: 100%; height: 100px; background: #000; color: #fff; border: 1px solid #444; padding: 10px; font-size: 16px; margin-bottom: 10px; }
        button { background: #00ff41; color: #000; border: none; padding: 10px 20px; font-size: 18px; cursor: pointer; font-weight: bold; width: 100%; }
        button:hover { background: #00cc33; }
        button:disabled { background: #555; cursor: not-allowed; }
        #status { margin-top: 20px; text-align: left; background: #000; padding: 10px; border: 1px solid #333; height: 150px; overflow-y: auto; font-size: 12px; }
        .log-entry { margin-bottom: 5px; border-bottom: 1px solid #222; padding-bottom: 2px; }
        .error { color: #ff3333; }
        .success { color: #00ff41; }
        .nav-link { display: inline-block; margin: 10px; color: #00ff41; text-decoration: none; font-size: 14px; }
        .nav-link:hover { text-decoration: underline; }
        
        /* My History Section */
        .my-history { margin-top: 30px; padding-top: 20px; border-top: 2px solid #333; }
        .my-history h2 { font-size: 20px; margin-bottom: 15px; }
        .history-item { background: #0a0a0a; padding: 10px; margin: 8px 0; border-radius: 5px; border: 1px solid #333; display: flex; justify-content: space-between; align-items: center; }
        .history-prompt { flex: 1; text-align: left; color: #00ff41; font-size: 13px; }
        .history-time { color: #666; font-size: 11px; margin-bottom: 3px; }
        .history-actions { display: flex; gap: 5px; }
        .history-actions button { width: auto; padding: 5px 12px; font-size: 12px; }
        .btn-repeat { background: #00ccff; }
        .btn-repeat:hover { background: #0099cc; }
        .btn-undo { background: #ff9900; }
        .btn-undo:hover { background: #ff7700; }
        .empty-history { color: #666; font-style: italic; font-size: 13px; padding: 20px; }
    </style>
</head>
<body>
    <div class="container">
        <h1>AI CHAOS CONTROL</h1>
        
        <div class="main-controls">
            <p>Type a request to mess with the streamer.</p>
            <textarea id="promptInput" placeholder="Ex: Make everyone tiny..."></textarea>
            <button id="sendBtn" onclick="sendPrompt()">SEND CHAOS</button>
            
            <div id="status"></div>
        </div>
        
        <div class="my-history">
            <h2>📋 My Commands (This Session)</h2>
            <div id="myHistory">
                <div class="empty-history">Your commands will appear here...</div>
            </div>
        </div>
    </div>

    <script>
        const input = document.getElementById('promptInput');
        const btn = document.getElementById('sendBtn');
        const statusDiv = document.getElementById('status');
        const myHistoryDiv = document.getElementById('myHistory');
        
        // Store user's commands in localStorage
        let myCommands = JSON.parse(localStorage.getItem('myCommands') || '[]');
        
        function log(msg, type='normal') {
            const el = document.createElement('div');
            el.className = 'log-entry ' + type;
            el.innerText = `[${new Date().toLocaleTimeString()}] ${msg}`;
            statusDiv.prepend(el);
        }
        
        function renderMyHistory() {
            if (myCommands.length === 0) {
                myHistoryDiv.innerHTML = '<div class="empty-history">Your commands will appear here...</div>';
                return;
            }
            
            myHistoryDiv.innerHTML = '';
            
            // Show last 10 commands, newest first
            [...myCommands].reverse().slice(0, 10).forEach((cmd, index) => {
                const div = document.createElement('div');
                div.className = 'history-item';
                div.innerHTML = `
                    <div style="flex: 1;">
                        <div class="history-time">${cmd.time}</div>
                        <div class="history-prompt">${escapeHtml(cmd.prompt)}</div>
                    </div>
                    <div class="history-actions">
                        <button class="btn-repeat" onclick="repeatCommand(${myCommands.length - 1 - index})">🔁 Repeat</button>
                        ${cmd.commandId ? `<button class="btn-undo" onclick="undoCommand(${cmd.commandId})">↩ Undo</button>` : ''}
                    </div>
                `;
                myHistoryDiv.appendChild(div);
            });
        }
        
        async function sendPrompt() {
            const text = input.value.trim();
            if (!text) return;

            input.disabled = true;
            btn.disabled = true;
            btn.innerText = "GENERATING...";

            try {
                const response = await fetch('/trigger', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ prompt: text })
                });
                
                const data = await response.json();
                
                if (data.status === "queued") {
                    log(`SUCCESS: ${text}`, 'success');
                    log(`> AI Generated Code (queued)`, 'normal');
                    
                    // Add to my history
                    myCommands.push({
                        prompt: text,
                        time: new Date().toLocaleTimeString(),
                        commandId: data.command_id || null
                    });
                    localStorage.setItem('myCommands', JSON.stringify(myCommands));
                    renderMyHistory();
                    
                } else if (data.status === "ignored") {
                    log(`BLOCKED: ${data.message}`, 'error');
                } else {
                    log(`ERROR: ${JSON.stringify(data)}`, 'error');
                }
            } catch (err) {
                log(`NETWORK ERROR: ${err.message}`, 'error');
            }

            input.value = "";
            input.disabled = false;
            btn.disabled = false;
            btn.innerText = "SEND CHAOS";
            input.focus();
        }
        
        async function repeatCommand(index) {
            const cmd = myCommands[index];
            if (!cmd || !cmd.commandId) {
                log('Cannot repeat - command not found', 'error');
                return;
            }
            
            log(`Repeating: ${cmd.prompt}`, 'normal');
            
            try {
                const response = await fetch('/api/repeat', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ command_id: cmd.commandId })
                });
                
                const data = await response.json();
                if (data.status === 'success') {
                    log('✓ Command repeated!', 'success');
                } else {
                    log('✗ Repeat failed: ' + data.message, 'error');
                }
            } catch (err) {
                log('✗ Network error', 'error');
            }
        }
        
        async function undoCommand(commandId) {
            log('Undoing command...', 'normal');
            
            try {
                const response = await fetch('/api/undo', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ command_id: commandId })
                });
                
                const data = await response.json();
                if (data.status === 'success') {
                    log('✓ Command undone!', 'success');
                } else {
                    log('✗ Undo failed: ' + data.message, 'error');
                }
            } catch (err) {
                log('✗ Network error', 'error');
            }
        }
        
        function escapeHtml(text) {
            const div = document.createElement('div');
            div.textContent = text;
            return div.innerHTML;
        }
        
        // Allow Ctrl+Enter to submit
        input.addEventListener('keydown', function(e) {
            if (e.ctrlKey && e.key === 'Enter') {
                sendPrompt();
            }
        });
        
        // Render history on load
        renderMyHistory();
    </script>
</body>
</html>
"""

HISTORY_TEMPLATE = """
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>Command History - AI Chaos</title>
    <style>
        body { background-color: #121212; color: #00ff41; font-family: 'Courier New', monospace; padding: 20px; }
        h1 { text-shadow: 0 0 10px #00ff41; text-align: center; }
        .nav-link { display: inline-block; margin: 10px; color: #00ff41; text-decoration: none; }
        .nav-link:hover { text-decoration: underline; }
        .controls { text-align: center; margin: 20px 0; background: #1e1e1e; padding: 15px; border-radius: 8px; }
        .controls button { background: #00ff41; color: #000; border: none; padding: 8px 15px; margin: 5px; cursor: pointer; font-weight: bold; }
        .controls button:hover { background: #00cc33; }
        .controls label { margin: 0 10px; }
        .command-list { max-width: 1200px; margin: 0 auto; }
        .command-item { background: #1e1e1e; margin: 10px 0; padding: 15px; border-radius: 8px; border: 1px solid #333; }
        .command-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 10px; }
        .command-time { color: #888; font-size: 12px; }
        .command-prompt { color: #00ff41; font-size: 16px; font-weight: bold; }
        .command-actions { display: flex; gap: 10px; }
        .command-actions button { padding: 5px 10px; font-size: 12px; border: none; cursor: pointer; font-weight: bold; }
        .btn-undo { background: #ff9900; color: #000; }
        .btn-undo:hover { background: #ff7700; }
        .btn-force-undo { background: #ff3333; color: #fff; }
        .btn-force-undo:hover { background: #cc0000; }
        .btn-repeat { background: #00ccff; color: #000; }
        .btn-repeat:hover { background: #0099cc; }
        .btn-view { background: #555; color: #fff; }
        .btn-view:hover { background: #777; }
        .code-preview { background: #000; padding: 10px; border-radius: 5px; font-size: 11px; max-height: 150px; overflow-y: auto; display: none; margin-top: 10px; }
        .code-preview.visible { display: block; }
        .status-msg { color: #ffff00; font-size: 12px; margin-top: 5px; }
        .empty-state { text-align: center; color: #888; padding: 50px; }
    </style>
</head>
<body>
    <h1>📜 COMMAND HISTORY</h1>
    <div style="text-align: center;">
        <a href="/" class="nav-link">⬅ Back to Control Panel</a>
    </div>
    
    <div class="controls">
        <h3 style="margin-top: 0;">Settings</h3>
        <label>
            <input type="checkbox" id="includeHistory" onchange="toggleSetting('include_history_in_ai', this.checked)">
            Feed history to AI context
        </label>
        <label>
            <input type="checkbox" id="historyEnabled" onchange="toggleSetting('history_enabled', this.checked)">
            Track command history
        </label>
        <button onclick="clearHistory()" style="background: #ff3333; margin-left: 20px;">Clear All History</button>
        <button onclick="loadHistory()" style="background: #555;">Refresh</button>
    </div>
    
    <div class="command-list" id="commandList">
        <div class="empty-state">Loading history...</div>
    </div>

    <script>
        let commands = [];
        
        async function loadHistory() {
            try {
                const response = await fetch('/api/history');
                const data = await response.json();
                commands = data.history;
                
                // Update checkboxes
                document.getElementById('includeHistory').checked = data.preferences.include_history_in_ai;
                document.getElementById('historyEnabled').checked = data.preferences.history_enabled;
                
                renderCommands();
            } catch (err) {
                console.error('Failed to load history:', err);
            }
        }
        
        function renderCommands() {
            const container = document.getElementById('commandList');
            
            if (commands.length === 0) {
                container.innerHTML = '<div class="empty-state">No commands in history yet. Send some chaos first!</div>';
                return;
            }
            
            container.innerHTML = '';
            
            // Reverse to show newest first
            [...commands].reverse().forEach(cmd => {
                const div = document.createElement('div');
                div.className = 'command-item';
                div.innerHTML = `
                    <div class="command-header">
                        <div>
                            <div class="command-time">#${cmd.id} - ${cmd.timestamp}</div>
                            <div class="command-prompt">${escapeHtml(cmd.user_prompt)}</div>
                        </div>
                        <div class="command-actions">
                            <button class="btn-repeat" onclick="repeatCommand(${cmd.id})">🔁 Repeat</button>
                            <button class="btn-undo" onclick="undoCommand(${cmd.id})">↩ Undo</button>
                            <button class="btn-force-undo" onclick="forceUndoCommand(${cmd.id})">⚠ Force Undo</button>
                            <button class="btn-view" onclick="toggleCode(${cmd.id})">👁 View Code</button>
                        </div>
                    </div>
                    <div id="status-${cmd.id}" class="status-msg"></div>
                    <div id="code-${cmd.id}" class="code-preview">
                        <strong>Execution Code:</strong><br>
                        <pre>${escapeHtml(cmd.execution_code)}</pre>
                        <hr>
                        <strong>Undo Code:</strong><br>
                        <pre>${escapeHtml(cmd.undo_code)}</pre>
                    </div>
                `;
                container.appendChild(div);
            });
        }
        
        function toggleCode(id) {
            const codeDiv = document.getElementById(`code-${id}`);
            codeDiv.classList.toggle('visible');
        }
        
        async function repeatCommand(id) {
            const statusEl = document.getElementById(`status-${id}`);
            statusEl.textContent = 'Repeating command...';
            
            try {
                const response = await fetch('/api/repeat', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ command_id: id })
                });
                const data = await response.json();
                
                if (data.status === 'success') {
                    statusEl.textContent = '✓ Command repeated successfully!';
                    statusEl.style.color = '#00ff41';
                } else {
                    statusEl.textContent = '✗ Failed: ' + data.message;
                    statusEl.style.color = '#ff3333';
                }
            } catch (err) {
                statusEl.textContent = '✗ Network error';
                statusEl.style.color = '#ff3333';
            }
        }
        
        async function undoCommand(id) {
            const statusEl = document.getElementById(`status-${id}`);
            statusEl.textContent = 'Undoing command...';
            
            try {
                const response = await fetch('/api/undo', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ command_id: id })
                });
                const data = await response.json();
                
                if (data.status === 'success') {
                    statusEl.textContent = '✓ Command undone!';
                    statusEl.style.color = '#00ff41';
                } else {
                    statusEl.textContent = '✗ Failed: ' + data.message;
                    statusEl.style.color = '#ff3333';
                }
            } catch (err) {
                statusEl.textContent = '✗ Network error';
                statusEl.style.color = '#ff3333';
            }
        }
        
        async function forceUndoCommand(id) {
            const statusEl = document.getElementById(`status-${id}`);
            statusEl.textContent = 'AI is generating force undo...';
            
            try {
                const response = await fetch('/api/force_undo', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ command_id: id })
                });
                const data = await response.json();
                
                if (data.status === 'success') {
                    statusEl.textContent = '✓ Force undo executed!';
                    statusEl.style.color = '#00ff41';
                } else {
                    statusEl.textContent = '✗ Failed: ' + data.message;
                    statusEl.style.color = '#ff3333';
                }
            } catch (err) {
                statusEl.textContent = '✗ Network error';
                statusEl.style.color = '#ff3333';
            }
        }
        
        async function toggleSetting(setting, value) {
            try {
                await fetch('/api/preferences', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ [setting]: value })
                });
            } catch (err) {
                console.error('Failed to update setting:', err);
            }
        }
        
        async function clearHistory() {
            if (!confirm('Are you sure you want to clear all command history?')) return;
            
            try {
                await fetch('/api/clear_history', { method: 'POST' });
                loadHistory();
            } catch (err) {
                alert('Failed to clear history');
            }
        }
        
        function escapeHtml(text) {
            const div = document.createElement('div');
            div.textContent = text;
            return div.innerHTML;
        }
        
        // Load on page load
        loadHistory();
        
        // Auto-refresh every 5 seconds
        setInterval(loadHistory, 5000);
    </script>
</body>
</html>
"""

# ==========================================
# SETUP & MODEL LOADING
# ==========================================

nsfw_classifier = None
captioner = None
ocr_reader = None

if ENABLE_IMAGES:
    print("Loading Local AI Models... (This includes downloading them if first run)")
    try:
        # 1. Advanced NSFW Detector (AdamCodd)
        print(" - Loading Advanced NSFW Detector (AdamCodd)...")
        nsfw_classifier = pipeline("image-classification", model="AdamCodd/vit-base-nsfw-detector")
        
        # 2. Smarter Captioner (BLIP Large)
        print(" - Loading Large Captioner (BLIP)...")
        captioner = pipeline("image-to-text", model="Salesforce/blip-image-captioning-large")
        
        # 3. EasyOCR (No external exe needed)
        print(" - Loading EasyOCR...")
        ocr_reader = easyocr.Reader(['en'], verbose=False) 
        
        print("All Models Loaded Successfully!")

    except Exception as e:
        print(f"\033[91m[ERROR] Failed to load AI models: {e}\033[0m")
        print("Continuing with Image Scanning DISABLED.")
        ENABLE_IMAGES = False
else:
    print("Image Scanning is DISABLED in config. Skipping heavy model loading.")

# Load API Key
try:
    with open("openrouter_api_key.txt", "r") as f:
        OPENROUTER_API_KEY = f.read().strip()
except FileNotFoundError:
    print("\033[91m[ERROR] openrouter_api_key.txt not found!\033[0m")
    exit(1)

app = Flask(__name__)
client = OpenAI(
    base_url=YOUR_SITE_URL,
    api_key=OPENROUTER_API_KEY,
)

command_queue = []

# ==========================================
# COMMAND HISTORY & PREFERENCES
# ==========================================

command_history = []  # Stores all executed commands

user_preferences = {
    'include_history_in_ai': True,  # Whether to feed command history to AI
    'history_enabled': True,  # Whether to track history at all
    'max_history_length': 50,  # Maximum commands to keep in history
}

# --- LOCAL SCANNING LOGIC ---
def sanitize_and_scan(user_prompt):
    """
    Returns a tuple: (cleaned_prompt, image_context_string, was_blocked)
    """
    # If images are disabled, simply return the prompt as is (or strip URLs if you prefer)
    if not ENABLE_IMAGES:
        return user_prompt, "", False

    url_pattern = r'http[s]?://(?:[a-zA-Z]|[0-9]|[$-_@.&+]|[!*\\(\\),]|(?:%[0-9a-fA-F][0-9a-fA-F]))+'
    urls = re.findall(url_pattern, user_prompt)
    
    clean_prompt = user_prompt
    extracted_context = []
    was_blocked = False

    for url in urls:
        parsed = urlparse(url)
        domain = parsed.netloc.lower()
        
        # 1. Basic Whitelist Check
        is_trusted = any(trusted in domain for trusted in TRUSTED_DOMAINS)
        if not is_trusted:
            print(f"[SAFETY] Blocked untrusted domain: {domain}")
            clean_prompt = clean_prompt.replace(url, PLACEHOLDER_IMAGE)
            was_blocked = True
            continue 

        # 2. Local AI Scan
        print(f"[SAFETY] Downloading image for local scan: {url}")
        try:
            # Download image to memory with browser spoofing
            headers = {'User-Agent': 'Mozilla/5.0'}
            response = requests.get(url, timeout=5, headers=headers)
            if response.status_code != 200:
                raise Exception(f"HTTP Error {response.status_code}")
            
            # Convert to PIL Image
            img = Image.open(io.BytesIO(response.content)).convert("RGB")

            # A. ADVANCED NSFW CHECK
            safety_results = nsfw_classifier(img)
            scores = {item['label']: item['score'] for item in safety_results}
            score_nsfw = scores.get('nsfw', 0)
            score_sexy = scores.get('sexy', 0)

            if score_sexy > THRESHOLD_SEXY:
                print(f"[SAFETY] BLOCKED SUGGESTIVE IMAGE! Confidence: {score_sexy:.2f}")
                clean_prompt = clean_prompt.replace(url, PLACEHOLDER_IMAGE)
                extracted_context.append("User tried to show suggestive content (Blocked).")
                was_blocked = True
                continue

            if score_nsfw > THRESHOLD_NSFW:
                print(f"[SAFETY] BLOCKED HARDCORE IMAGE! Confidence: {score_nsfw:.2f}")
                clean_prompt = clean_prompt.replace(url, PLACEHOLDER_IMAGE)
                extracted_context.append("User tried to show explicit NSFW content (Blocked).")
                was_blocked = True
                continue

            print(f"[SAFETY] Image passed NSFW check. (NSFW: {score_nsfw:.2f}, Sexy: {score_sexy:.2f})")

            # B. OCR (EASYOCR)
            img_np = np.array(img)
            text_list = ocr_reader.readtext(img_np, detail=0)
            raw_text = " ".join(text_list)
            
            if any(word in raw_text.lower() for word in BAD_WORDS_BLOCKLIST):
                print(f"[SAFETY] BLOCKED IMAGE TEXT! Found blocked word.")
                clean_prompt = clean_prompt.replace(url, PLACEHOLDER_IMAGE)
                was_blocked = True
                continue
            
            # C. IMAGE CAPTIONING (BLIP LARGE)
            caption_result = captioner(img, max_new_tokens=50)
            description = caption_result[0]['generated_text']
            
            print(f"[CONTEXT] Image content: '{description}'")
            if len(raw_text) > 0:
                print(f"[CONTEXT] Image text: '{raw_text}'")
            
            context_str = f"Image shows: '{description}'."
            if len(raw_text) > 2:
                context_str += f" Image contains text: '{raw_text}'."
            
            extracted_context.append(context_str)

        except Exception as e:
            print(f"[SAFETY] Scan error ({e}). Blocking to be safe.")
            clean_prompt = clean_prompt.replace(url, PLACEHOLDER_IMAGE)
            was_blocked = True

    return clean_prompt, " ".join(extracted_context), was_blocked

# --- PROMPT CONSTRUCTION ---

PROMPT_CORE = """
You are an expert Lua scripter for Garry's Mod (GLua). 
You will receive a request from a livestream chat and the current map name. 
The chat is controlling the streamer's playthrough of Half-Life 2 via your generated scripts.
Generate valid GLua code to execute that request immediately.

**IMPORTANT: You must return TWO code blocks separated by '---UNDO---':**
1. The EXECUTION code (what the user requested)
2. The UNDO code (code to reverse/stop the effect)

The undo code should completely reverse any changes, stop timers, remove entities, restore original values, etc.

GROUND RULES:
1. **Server vs Client Architecture:**
   - You are executing in a SERVER environment.
   - For Physics, Health, Entities, Spawning, and Gravity: Write standard code.
   - For **UI, HUD, Screen Effects, or Client Sounds**: You CANNOT write them directly. You MUST wrap that specific code inside `RunOnClient([[ ... ]])`.
   - *Note:* `LocalPlayer()` is only valid inside the `RunOnClient` wrapper. On the server layer, use `player.GetAll()` or `Entity(1)`.

2. **Temporary Effects:** If the effect is disruptive (blindness, gravity, speed, spawning enemies, screen overlays), you MUST wrap a reversion in a 'timer.Simple'. 
   - Light effects: Can be permanent. (spawning one or a few props/friendly npcs, changing walk speed slightly, chat messages)
   - Mild effects: 15 seconds to 1 minute.
   - Heavy/Chaos effects: 5-10 seconds.

3. **Anti-Softlock:** NEVER use 'entity:Remove()' on key story objects, NPCs, or generic searches (like looping through all ents). 
   - Instead, use 'SetNoDraw(true)' and 'SetCollisionGroup(COLLISION_GROUP_IN_VEHICLE)' to hide them, then revert it in the timer.
   - For model swaps, you can use a bonemerge and temporarily hide the original model. this is a softlock safe way to change appearances.  

4. **Safety:** Do not use 'os.execute', 'http.Fetch' (outbound), or file system writes. Do not crash the server, but feel free to temporarily lag it or spawn many entities (limit to 100) for comedic effect.

5. **Humor:** If a request is malicious (e.g., "Dox the streamer"), do a fake version (but don't say it's fake). You can be really relaxed about the rules if the intent is comedic.
   - Example: RunOnClient([=[ chat.AddText(Color(255,0,0), "217.201.21.8") ]=])

6. **POV Awareness:** Try to make sure things happen where the player can see them (unless otherwise stated for comedic effect). For example, spawning something in front of the player rather than behind them or at world origin.

7. **UI:** Make sure you can interact with UI elements and popups that require it! (MakePopup())
   -You can do advanced UI in HTML, for better effects and fancy styling and js.
"""

PROMPT_IMAGES = """
8. **Image Context Awareness:**
   - If you receive [SYSTEM DETECTED IMAGE CONTEXT], it means the user linked an image containing specific text or objects.
   - Use this context creatively! If the image context says "Image shows: a spooky ghost", spawn a zombie or play a scream sound.
   - If the user simply asks to "Show this image", pass the URL provided in the prompt to a HTML Panel inside `RunOnClient`.
   - Images have been pre-scanned for safety by a small and somewhat inaccurate AI. use caution if the context seems suspicious.
"""

PROMPT_FOOTER = """
8. **Output:** RETURN ONLY THE RAW LUA CODE. Do not include markdown backticks (```lua) or explanations.
   Format: EXECUTION_CODE
   ---UNDO---
   UNDO_CODE

--- EXAMPLES ---

INPUT: "Make everyone tiny"
OUTPUT:
for _, v in pairs(player.GetAll()) do 
    v:SetModelScale(0.2, 1) 
end
timer.Simple(10, function()
    for _, v in pairs(player.GetAll()) do 
        v:SetModelScale(1, 1) 
    end
end)
---UNDO---
for _, v in pairs(player.GetAll()) do 
    v:SetModelScale(1, 1) 
end
for k, v in pairs(timer.GetTimers()) do
    if v and string.find(k, "Simple") then timer.Remove(k) end
end

INPUT: "Disable gravity"
OUTPUT:
RunConsoleCommand("sv_gravity", "0")
timer.Simple(10, function() RunConsoleCommand("sv_gravity", "600") end)

INPUT: "Make the screen go black for 5 seconds"
OUTPUT:
RunOnClient([=[
    local black = vgui.Create("DPanel")
    black:SetSize(ScrW(), ScrH())
    black:SetBackgroundColor(Color(0,0,0))
    black:Center()
    timer.Simple(5, function() if IsValid(black) then black:Remove() end end)
]=])

INPUT: "Show this image: https://imgur.com/cat.jpg"
CONTEXT: "Image shows: a cute cat."
OUTPUT:
-- We use [=[ and ]=] for the wrapper. We do NOT use MakePopup() as that takes control away.
RunOnClient([=[
    local html = vgui.Create("DHTML")
    html:SetSize(500, 500)
    html:Center()
    -- Using inline CSS to make image fit perfectly
    html:SetHTML([[ <body style="margin:0;overflow:hidden"><img src="https://imgur.com/cat.jpg" style="width:100%;height:100%;object-fit:contain"></body> ]])
    
    surface.PlaySound("garrysmod/content_downloaded.wav")
    
    timer.Simple(10, function() if IsValid(html) then html:Remove() end end)
]=])

INPUT: "Show a message box that types out 'The chat has taken control of your destiny...'"
OUTPUT:
RunOnClient([=[
    local text = "* The chat has taken control of your destiny..."
    local currentChar = 0
    local typingSound = "buttons/button17.wav"
    
    local box = vgui.Create("DPanel")
    box:SetSize(600, 100)
    box:SetPos((ScrW() - 600) / 2, ScrH() - 150)
    
    box.Paint = function(self, w, h)
        -- Black box with white border
        surface.SetDrawColor(0, 0, 0, 250)
        surface.DrawRect(0, 0, w, h)
        surface.SetDrawColor(255, 255, 255, 255)
        surface.DrawOutlinedRect(0, 0, w, h, 2)
    end
    
    local textLabel = vgui.Create("DLabel", box)
    textLabel:SetFont("Trebuchet24")
    textLabel:SetTextColor(Color(255, 255, 255))
    textLabel:SetPos(20, 20)
    textLabel:SetSize(560, 60)
    textLabel:SetText("")
    
    -- Type out the text character by character
    local function TypeText()
        if currentChar < #text then
            currentChar = currentChar + 1
            textLabel:SetText(string.sub(text, 1, currentChar))
            surface.PlaySound(typingSound)
            timer.Simple(0.05, TypeText)
        end
    end
    
    TypeText()
    
    -- Remove after 8 seconds
    timer.Simple(8, function()
        if IsValid(box) then box:Remove() end
    end)
]=])
"""

# Construct the final system prompt based on config
if ENABLE_IMAGES:
    SYSTEM_PROMPT = PROMPT_CORE + PROMPT_IMAGES + PROMPT_FOOTER 
else:
    SYSTEM_PROMPT = PROMPT_CORE + PROMPT_FOOTER


def generate_lua(user_request, current_map, image_context, include_history=True):
    full_user_content = f"Current Map: {current_map}. Request: {user_request}"
    
    if ENABLE_IMAGES and image_context:
        full_user_content += f"\n[SYSTEM DETECTED IMAGE CONTEXT]: {image_context}"
    
    # Include recent command history if enabled
    if include_history and user_preferences['include_history_in_ai'] and command_history:
        recent_commands = command_history[-5:]  # Last 5 commands
        history_text = "\n\n[RECENT COMMAND HISTORY]:\n"
        for cmd in recent_commands:
            history_text += f"- {cmd['timestamp']}: {cmd['user_prompt']}\n"
        full_user_content += history_text

    try:
        completion = client.chat.completions.create(
            #model="anthropic/claude-3.5-sonnet", 
            model="anthropic/claude-sonnet-4.5",
            messages=[
                {"role": "system", "content": SYSTEM_PROMPT},
                {"role": "user", "content": full_user_content}
            ]
        )
        code = completion.choices[0].message.content.replace("```lua", "").replace("```", "").strip()
        
        # Parse execution and undo code
        if "---UNDO---" in code:
            parts = code.split("---UNDO---")
            execution_code = parts[0].strip()
            undo_code = parts[1].strip() if len(parts) > 1 else 'print("No undo code provided")'
            return execution_code, undo_code
        else:
            # Fallback if AI didn't follow format
            return code, 'print("Undo not available for this command")'
    except Exception as e:
        print(f"AI Error: {e}")
        return 'print("AI Generation Failed")', 'print("Undo not available")'

@app.route('/')
def index():
    return render_template_string(HTML_TEMPLATE)

@app.route('/poll', methods=['POST'])
def poll():
    # Use force=True to avoid 415 errors
    data = request.get_json(force=True, silent=True) or {}
    if command_queue:
        return jsonify({"has_code": True, "code": command_queue.pop(0)})
    return jsonify({"has_code": False})

@app.route('/trigger', methods=['POST'])
def trigger():
    data = request.get_json(force=True, silent=True) or {}
    user_request = data.get('prompt')
    if not user_request: return jsonify({"error": "No prompt"}), 400

    # 0. Block changelevel attempts
    changelevel_keywords = ['changelevel', 'change level', 'next map', 'load map', 'switch map', 'new map', 'RunConsoleCommand.*map']
    if any(keyword.lower() in user_request.lower() for keyword in changelevel_keywords):
        print(f"[SAFETY] Blocked changelevel attempt: {user_request}")
        return jsonify({
            "status": "ignored", 
            "message": "Map/level changing is blocked for safety."
        })

    # 1. Sanitize & Scan (Blocks bad images, extracts text/context)
    # If ENABLE_IMAGES is false, this returns immediately with empty context
    safe_request, image_context, was_blocked = sanitize_and_scan(user_request)

    if was_blocked:
        print(f"[SAFETY] Ignoring Request: {user_request}")
        return jsonify({
            "status": "ignored", 
            "message": "Safety protocols blocked this request."
        })
    
    current_map = "unknown" 
    
    print(f"Generating code for: {safe_request}")
    if image_context: print(f"Context: {image_context}")

    # 2. Generate Code with Undo
    execution_code, undo_code = generate_lua(safe_request, current_map, image_context)
    
    # 2.5. Post-generation safety check for changelevel commands
    dangerous_patterns = ['changelevel', 'RunConsoleCommand.*"map"', 'RunConsoleCommand.*\'map\'', 'game.ConsoleCommand.*map']
    if any(re.search(pattern, execution_code, re.IGNORECASE) for pattern in dangerous_patterns):
        print(f"[SAFETY] AI tried to generate changelevel code - blocking!")
        return jsonify({
            "status": "ignored", 
            "message": "Generated code attempted to change map (blocked)."
        })
    
    command_queue.append(execution_code)
    
    # 3. Store in history if enabled
    if user_preferences['history_enabled']:
        command_entry = {
            'id': len(command_history) + 1,
            'timestamp': time.strftime('%Y-%m-%d %H:%M:%S'),
            'user_prompt': safe_request,
            'execution_code': execution_code,
            'undo_code': undo_code,
            'image_context': image_context,
            'status': 'executed'
        }
        command_history.append(command_entry)
        
        # Trim history if too long
        if len(command_history) > user_preferences['max_history_length']:
            command_history.pop(0)
    
    return jsonify({
        "status": "queued", 
        "code_preview": execution_code,
        "has_undo": True,
        "command_id": len(command_history) if user_preferences['history_enabled'] else None,
        "context_found": image_context,
        "was_blocked": was_blocked
    })

@app.route('/scan_test', methods=['POST'])
def scan_test():
    """
    Endpoint to manually test if an image is considered safe and what the AI sees.
    """
    if not ENABLE_IMAGES:
        return jsonify({"error": "Image processing is disabled in configuration."}), 400

    data = request.get_json(force=True, silent=True) or {}
    url_to_test = data.get('url')
    
    if not url_to_test:
        return jsonify({"error": "No url provided"}), 400

    print(f"--- TESTING IMAGE: {url_to_test} ---")
    prompt_dummy = f"Test scan {url_to_test}"
    clean_prompt, context, was_blocked = sanitize_and_scan(prompt_dummy)
    
    return jsonify({
        "original_url": url_to_test,
        "blocked": was_blocked,
        "replaced_url": PLACEHOLDER_IMAGE if was_blocked else url_to_test,
        "ai_context_extracted": context
    })

# ==========================================
# HISTORY & MANAGEMENT ENDPOINTS
# ==========================================

@app.route('/history')
def history_page():
    """Render the command history UI page"""
    return render_template_string(HISTORY_TEMPLATE)

@app.route('/api/history', methods=['GET'])
def get_history():
    """Get all command history and preferences"""
    return jsonify({
        'history': command_history,
        'preferences': user_preferences
    })

@app.route('/api/repeat', methods=['POST'])
def repeat_command():
    """Repeat a previous command without regenerating code"""
    data = request.get_json(force=True, silent=True) or {}
    command_id = data.get('command_id')
    
    if not command_id:
        return jsonify({'status': 'error', 'message': 'No command ID provided'}), 400
    
    # Find the command in history
    command = next((cmd for cmd in command_history if cmd['id'] == command_id), None)
    
    if not command:
        return jsonify({'status': 'error', 'message': 'Command not found in history'}), 404
    
    # Re-queue the execution code
    command_queue.append(command['execution_code'])
    
    print(f"[REPEAT] Re-executing command #{command_id}: {command['user_prompt']}")
    
    return jsonify({
        'status': 'success',
        'message': 'Command re-queued for execution',
        'command_id': command_id
    })

@app.route('/api/undo', methods=['POST'])
def undo_command():
    """Execute the undo code for a specific command"""
    data = request.get_json(force=True, silent=True) or {}
    command_id = data.get('command_id')
    
    if not command_id:
        return jsonify({'status': 'error', 'message': 'No command ID provided'}), 400
    
    # Find the command in history
    command = next((cmd for cmd in command_history if cmd['id'] == command_id), None)
    
    if not command:
        return jsonify({'status': 'error', 'message': 'Command not found in history'}), 404
    
    # Queue the undo code
    command_queue.append(command['undo_code'])
    
    print(f"[UNDO] Executing undo for command #{command_id}: {command['user_prompt']}")
    
    return jsonify({
        'status': 'success',
        'message': 'Undo code queued for execution',
        'command_id': command_id
    })

@app.route('/api/force_undo', methods=['POST'])
def force_undo_command():
    """Use AI to generate a stronger undo if the regular one isn't working"""
    data = request.get_json(force=True, silent=True) or {}
    command_id = data.get('command_id')
    
    if not command_id:
        return jsonify({'status': 'error', 'message': 'No command ID provided'}), 400
    
    # Find the command in history
    command = next((cmd for cmd in command_history if cmd['id'] == command_id), None)
    
    if not command:
        return jsonify({'status': 'error', 'message': 'Command not found in history'}), 404
    
    # Generate a force undo using AI
    force_undo_prompt = f"""The following command is still causing problems and needs to be forcefully stopped:

Original Request: {command['user_prompt']}
Original Code: {command['execution_code']}
Previous Undo Attempt: {command['undo_code']}

This is still a problem. Generate comprehensive Lua code to:
1. Stop ALL timers that might be related
2. Remove ALL entities that were spawned
3. Reset ALL player properties to default
4. Clear ALL screen effects and UI elements
5. Restore normal game state

Be aggressive - we need to ensure this effect is completely gone.
Return ONLY the Lua code to execute, no explanations."""

    print(f"[FORCE UNDO] Generating AI solution for command #{command_id}")
    
    try:
        completion = client.chat.completions.create(
            model="anthropic/claude-sonnet-4.5",
            messages=[
                {"role": "system", "content": "You are a Garry's Mod Lua expert. Generate code to completely stop and reverse problematic effects."},
                {"role": "user", "content": force_undo_prompt}
            ]
        )
        force_undo_code = completion.choices[0].message.content.replace("```lua", "").replace("```", "").strip()
        
        # Queue the force undo code
        command_queue.append(force_undo_code)
        
        print(f"[FORCE UNDO] Generated and queued force undo code")
        
        return jsonify({
            'status': 'success',
            'message': 'AI-generated force undo queued',
            'command_id': command_id,
            'force_undo_code': force_undo_code
        })
        
    except Exception as e:
        print(f"[FORCE UNDO] AI Error: {e}")
        return jsonify({'status': 'error', 'message': f'AI generation failed: {str(e)}'}), 500

@app.route('/api/preferences', methods=['POST'])
def update_preferences():
    """Update user preferences"""
    data = request.get_json(force=True, silent=True) or {}
    
    for key in ['include_history_in_ai', 'history_enabled', 'max_history_length']:
        if key in data:
            user_preferences[key] = data[key]
    
    print(f"[PREFERENCES] Updated: {data}")
    
    return jsonify({
        'status': 'success',
        'preferences': user_preferences
    })

@app.route('/api/clear_history', methods=['POST'])
def clear_history():
    """Clear all command history"""
    global command_history
    command_history = []
    
    print("[HISTORY] Command history cleared")
    
    return jsonify({
        'status': 'success',
        'message': 'History cleared'
    })

if __name__ == '__main__':
    print(f"Brain is running on port 5000... (Images Enabled: {ENABLE_IMAGES})")
    print(f"Open http://127.0.0.1:5000/ in your browser to test!")
    app.run(port=5000)