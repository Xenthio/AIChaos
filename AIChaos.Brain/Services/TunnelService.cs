using System.Diagnostics;
using System.Text.RegularExpressions;
using AIChaos.Brain.Models;

namespace AIChaos.Brain.Services;

/// <summary>
/// Service for managing tunnel connections (ngrok, localtunnel, bore).
/// </summary>
public partial class TunnelService : IDisposable
{
    private readonly SettingsService _settingsService;
    private readonly ILogger<TunnelService> _logger;
    private readonly HttpClient _httpClient;
    
    private Process? _tunnelProcess;
    
    public bool IsRunning => _tunnelProcess != null && !_tunnelProcess.HasExited;
    public string? CurrentUrl { get; private set; }
    public TunnelType CurrentType { get; private set; } = TunnelType.None;
    public string? PublicIp { get; private set; }
    
    public TunnelService(
        SettingsService settingsService,
        ILogger<TunnelService> logger,
        IHttpClientFactory httpClientFactory)
    {
        _settingsService = settingsService;
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient();
    }
    
    /// <summary>
    /// Starts an ngrok tunnel.
    /// </summary>
    public async Task<(bool Success, string? Url, string? Error)> StartNgrokAsync()
    {
        if (IsRunning)
        {
            return (false, null, "A tunnel is already running. Stop it first.");
        }
        
        try
        {
            // Check if ngrok is installed
            var checkResult = await RunCommandAsync("ngrok", "version");
            if (!checkResult.Success)
            {
                return (false, null, "ngrok is not installed. Please install it from https://ngrok.com/download and run 'ngrok config add-authtoken YOUR_TOKEN' to configure it.");
            }
            
            // Start ngrok
            _tunnelProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "ngrok",
                    Arguments = "http 5000",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };
            
            _tunnelProcess.Start();
            _logger.LogInformation("ngrok started with PID {Pid}", _tunnelProcess.Id);
            
            // Wait for ngrok to initialize and get URL from API
            await Task.Delay(3000);
            
            var url = await GetNgrokUrlAsync();
            if (url == null)
            {
                Stop();
                return (false, null, "Failed to get ngrok URL. Make sure ngrok is properly configured with 'ngrok config add-authtoken YOUR_TOKEN'.");
            }
            
            CurrentUrl = url;
            CurrentType = TunnelType.Ngrok;
            
            // Update settings
            var tunnel = _settingsService.Settings.Tunnel;
            tunnel.Type = TunnelType.Ngrok;
            tunnel.CurrentUrl = url;
            tunnel.IsRunning = true;
            _settingsService.UpdateTunnel(tunnel);
            
            // Update Lua file
            await UpdateLuaFileAsync(url);
            
            return (true, url, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start ngrok");
            return (false, null, ex.Message);
        }
    }
    
    /// <summary>
    /// Starts a localtunnel.
    /// </summary>
    public async Task<(bool Success, string? Url, string? Error)> StartLocalTunnelAsync()
    {
        if (IsRunning)
        {
            return (false, null, "A tunnel is already running. Stop it first.");
        }
        
        try
        {
            // Check if localtunnel is installed
            var checkResult = await RunCommandAsync("lt", "--version");
            if (!checkResult.Success)
            {
                return (false, null, "localtunnel is not installed. Install it with: npm install -g localtunnel");
            }
            
            // Get public IP for localtunnel password
            await GetPublicIpAsync();
            
            // Start localtunnel
            _tunnelProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "lt",
                    Arguments = "--port 5000",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };
            
            _tunnelProcess.Start();
            _logger.LogInformation("localtunnel started with PID {Pid}", _tunnelProcess.Id);
            
            // Read output to get URL
            var url = await GetLocalTunnelUrlAsync();
            if (url == null)
            {
                Stop();
                return (false, null, "Failed to get localtunnel URL.");
            }
            
            CurrentUrl = url;
            CurrentType = TunnelType.LocalTunnel;
            
            // Update settings
            var tunnel = _settingsService.Settings.Tunnel;
            tunnel.Type = TunnelType.LocalTunnel;
            tunnel.CurrentUrl = url;
            tunnel.IsRunning = true;
            _settingsService.UpdateTunnel(tunnel);
            
            // Update Lua file
            await UpdateLuaFileAsync(url);
            
            return (true, url, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start localtunnel");
            return (false, null, ex.Message);
        }
    }
    
    /// <summary>
    /// Starts a Bore tunnel (bore.pub).
    /// </summary>
    public async Task<(bool Success, string? Url, string? Error)> StartBoreAsync()
    {
        if (IsRunning)
        {
            return (false, null, "A tunnel is already running. Stop it first.");
        }
        
        try
        {
            // Check if bore is installed
            var checkResult = await RunCommandAsync("bore", "--version");
            if (!checkResult.Success)
            {
                return (false, null, "bore is not installed. Install it with: cargo install bore-cli (requires Rust)");
            }
            
            // Start bore - bore local <port> --to bore.pub
            _tunnelProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "bore",
                    Arguments = "local 5000 --to bore.pub",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };
            
            _tunnelProcess.Start();
            _logger.LogInformation("bore started with PID {Pid}", _tunnelProcess.Id);
            
            // Read output to get URL (bore outputs to stderr)
            var url = await GetBoreUrlAsync();
            if (url == null)
            {
                Stop();
                return (false, null, "Failed to get bore URL. Make sure bore.pub is accessible.");
            }
            
            CurrentUrl = url;
            CurrentType = TunnelType.Bore;
            
            // Update settings
            var tunnel = _settingsService.Settings.Tunnel;
            tunnel.Type = TunnelType.Bore;
            tunnel.CurrentUrl = url;
            tunnel.IsRunning = true;
            _settingsService.UpdateTunnel(tunnel);
            
            // Update Lua file
            await UpdateLuaFileAsync(url);
            
            return (true, url, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start bore");
            return (false, null, ex.Message);
        }
    }
    
    /// <summary>
    /// Stops the current tunnel.
    /// </summary>
    public void Stop()
    {
        if (_tunnelProcess != null)
        {
            try
            {
                if (!_tunnelProcess.HasExited)
                {
                    _tunnelProcess.Kill(true);
                    _tunnelProcess.WaitForExit(5000);
                }
                _tunnelProcess.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping tunnel process");
            }
            _tunnelProcess = null;
        }
        
        CurrentUrl = null;
        CurrentType = TunnelType.None;
        
        // Update settings
        var tunnel = _settingsService.Settings.Tunnel;
        tunnel.IsRunning = false;
        tunnel.CurrentUrl = "";
        _settingsService.UpdateTunnel(tunnel);
        
        _logger.LogInformation("Tunnel stopped");
    }
    
    /// <summary>
    /// Stops the current tunnel (alias for Stop).
    /// </summary>
    public void StopTunnel() => Stop();
    
    /// <summary>
    /// Starts a tunnel based on the type name.
    /// </summary>
    public async Task<(bool Success, string? Url, string? Message)> StartTunnelAsync(string type)
    {
        return type.ToLowerInvariant() switch
        {
            "ngrok" => await StartNgrokAsync(),
            "localtunnel" => await StartLocalTunnelAsync(),
            "bore" => await StartBoreAsync(),
            _ => (false, null, $"Unknown tunnel type: {type}")
        };
    }
    
    /// <summary>
    /// Gets the current tunnel status.
    /// </summary>
    public TunnelStatus GetStatus()
    {
        return new TunnelStatus
        {
            IsRunning = IsRunning,
            Type = CurrentType,
            Url = CurrentUrl,
            PublicIp = PublicIp
        };
    }
    
    private async Task<string?> GetNgrokUrlAsync()
    {
        for (int i = 0; i < 10; i++)
        {
            try
            {
                var response = await _httpClient.GetAsync("http://localhost:4040/api/tunnels");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var match = NgrokUrlRegex().Match(json);
                    if (match.Success)
                    {
                        var url = match.Groups[1].Value;
                        // Force HTTPS
                        url = url.Replace("http://", "https://");
                        return url;
                    }
                }
            }
            catch
            {
                // Ignore and retry
            }
            await Task.Delay(1000);
        }
        return null;
    }
    
    private async Task<string?> GetLocalTunnelUrlAsync()
    {
        if (_tunnelProcess == null) return null;
        
        var timeout = DateTime.UtcNow.AddSeconds(30);
        while (DateTime.UtcNow < timeout)
        {
            if (_tunnelProcess.HasExited)
            {
                return null;
            }
            
            var line = await _tunnelProcess.StandardOutput.ReadLineAsync();
            if (!string.IsNullOrEmpty(line))
            {
                _logger.LogDebug("localtunnel output: {Line}", line);
                var match = UrlRegex().Match(line);
                if (match.Success)
                {
                    return match.Value;
                }
            }
        }
        return null;
    }
    
    private async Task<string?> GetBoreUrlAsync()
    {
        if (_tunnelProcess == null) return null;
        
        var output = new System.Text.StringBuilder();
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        
        try
        {
            // Read from both stdout and stderr concurrently with timeout
            // Bore can output to either stream depending on version
            var stdoutTask = ReadStreamWithTimeoutAsync(_tunnelProcess.StandardOutput, output, cts.Token);
            var stderrTask = ReadStreamWithTimeoutAsync(_tunnelProcess.StandardError, output, cts.Token);
            
            // Wait for either task to find a URL or timeout
            while (!cts.Token.IsCancellationRequested)
            {
                if (_tunnelProcess.HasExited)
                {
                    _logger.LogWarning("bore process exited prematurely");
                    break;
                }
                
                // Check current output for bore URL
                var currentOutput = output.ToString();
                if (!string.IsNullOrEmpty(currentOutput))
                {
                    _logger.LogDebug("bore output: {Output}", currentOutput);
                    var match = BorePortRegex().Match(currentOutput);
                    if (match.Success)
                    {
                        var port = match.Groups[1].Value;
                        cts.Cancel(); // Stop reading
                        return $"http://bore.pub:{port}";
                    }
                }
                
                await Task.Delay(500, cts.Token);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("bore URL detection timed out. Output received: {Output}", output.ToString());
        }
        
        return null;
    }
    
    private async Task ReadStreamWithTimeoutAsync(StreamReader reader, System.Text.StringBuilder output, CancellationToken ct)
    {
        var buffer = new char[1024];
        try
        {
            while (!ct.IsCancellationRequested)
            {
                // Use a small buffer read that won't block forever
                var readTask = reader.ReadAsync(buffer, 0, buffer.Length);
                var completedTask = await Task.WhenAny(readTask, Task.Delay(100, ct));
                
                if (completedTask == readTask)
                {
                    var bytesRead = await readTask;
                    if (bytesRead > 0)
                    {
                        lock (output)
                        {
                            output.Append(buffer, 0, bytesRead);
                        }
                    }
                    else
                    {
                        // Stream ended
                        break;
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when we find the URL or timeout
        }
        catch (Exception ex)
        {
            _logger.LogDebug("Error reading bore stream: {Error}", ex.Message);
        }
    }
    
    private async Task GetPublicIpAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("https://api.ipify.org");
            if (response.IsSuccessStatusCode)
            {
                PublicIp = (await response.Content.ReadAsStringAsync()).Trim();
                return;
            }
        }
        catch { }
        
        try
        {
            var response = await _httpClient.GetAsync("https://icanhazip.com");
            if (response.IsSuccessStatusCode)
            {
                PublicIp = (await response.Content.ReadAsStringAsync()).Trim();
            }
        }
        catch { }
    }
    
    private async Task UpdateLuaFileAsync(string url)
    {
        var pollUrl = $"{url}/poll";
        
        // Write to tunnel_url.txt - write the BASE URL (without /poll) because
        // the Lua script appends /poll itself when reading from this file
        await WriteTunnelUrlFileAsync(url);
        
        // Try multiple possible paths to find the Lua file
        var possiblePaths = new[]
        {
            // Relative to the project directory (when running from source)
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "lua", "autorun", "ai_chaos_controller.lua"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "lua", "autorun", "ai_chaos_controller.lua"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "lua", "autorun", "ai_chaos_controller.lua"),
            Path.Combine(AppContext.BaseDirectory, "..", "lua", "autorun", "ai_chaos_controller.lua"),
            // Relative to current working directory
            Path.Combine(Directory.GetCurrentDirectory(), "..", "lua", "autorun", "ai_chaos_controller.lua"),
            Path.Combine(Directory.GetCurrentDirectory(), "lua", "autorun", "ai_chaos_controller.lua"),
        };
        
        string? luaPath = null;
        foreach (var path in possiblePaths)
        {
            var fullPath = Path.GetFullPath(path);
            if (File.Exists(fullPath))
            {
                luaPath = fullPath;
                break;
            }
        }
        
        if (luaPath == null)
        {
            _logger.LogWarning("Lua file not found in any expected location. GMod addon should be in ../lua/ relative to AIChaos.Brain");
            _logger.LogWarning("Tunnel URL for GMod: {Url} - Please update your Lua file manually or copy it to the correct location", pollUrl);
            return;
        }
        
        try
        {
            var content = await File.ReadAllTextAsync(luaPath);
            
            // Update BASE_URL (without /poll)
            var baseUrlLine = $"    local BASE_URL = \"{url}\" -- Auto-configured by launcher";
            content = BaseUrlRegex().Replace(content, baseUrlLine);
            
            // Update SERVER_URL (with /poll)
            var serverUrlLine = $"    local SERVER_URL = \"{pollUrl}\" -- Auto-configured by launcher";
            content = ServerUrlRegex().Replace(content, serverUrlLine);
            
            await File.WriteAllTextAsync(luaPath, content);
            _logger.LogInformation("Updated Lua file with URL: {Url}", pollUrl);
            _logger.LogInformation("Lua file location: {Path}", luaPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update Lua file");
        }
    }
    
    private async Task WriteTunnelUrlFileAsync(string baseUrl)
    {
        // Write to tunnel_url.txt in multiple locations for compatibility
        // Note: We write the BASE URL (without /poll) - the Lua script appends /poll when reading
        var possibleDirs = new[]
        {
            // Root of repository (where the old Python scripts would write it)
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."),
            Path.Combine(AppContext.BaseDirectory, "..", "..", ".."),
            Path.Combine(AppContext.BaseDirectory, "..", ".."),
            Path.Combine(AppContext.BaseDirectory, ".."),
            // Current working directory
            Directory.GetCurrentDirectory(),
            Path.Combine(Directory.GetCurrentDirectory(), ".."),
        };
        
        foreach (var dir in possibleDirs)
        {
            try
            {
                var fullDir = Path.GetFullPath(dir);
                var tunnelUrlPath = Path.Combine(fullDir, "tunnel_url.txt");
                
                // Only write to directories that exist and where the lua folder exists (indicates repo root)
                var luaDir = Path.Combine(fullDir, "lua");
                if (Directory.Exists(fullDir) && Directory.Exists(luaDir))
                {
                    await File.WriteAllTextAsync(tunnelUrlPath, baseUrl);
                    _logger.LogInformation("Written tunnel URL to: {Path}", tunnelUrlPath);
                    return;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug("Could not write tunnel_url.txt to {Dir}: {Error}", dir, ex.Message);
            }
        }
        
        // Fallback: write to current directory
        try
        {
            var fallbackPath = Path.Combine(Directory.GetCurrentDirectory(), "tunnel_url.txt");
            await File.WriteAllTextAsync(fallbackPath, baseUrl);
            _logger.LogInformation("Written tunnel URL to fallback location: {Path}", fallbackPath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Failed to write tunnel_url.txt: {Error}", ex.Message);
        }
    }
    
    private async Task<(bool Success, string Output)> RunCommandAsync(string command, string arguments)
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = command,
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };
            
            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            
            return (process.ExitCode == 0, output + error);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }
    
    [GeneratedRegex(@"""public_url""\s*:\s*""(https?://[^""]+)""")]
    private static partial Regex NgrokUrlRegex();
    
    [GeneratedRegex(@"https://[^\s]+")]
    private static partial Regex UrlRegex();
    
    [GeneratedRegex(@"bore\.pub:(\d+)")]
    private static partial Regex BorePortRegex();
    
    [GeneratedRegex(@"local BASE_URL = "".*?"".*")]
    private static partial Regex BaseUrlRegex();
    
    [GeneratedRegex(@"local SERVER_URL = "".*?"".*")]
    private static partial Regex ServerUrlRegex();
    
    public void Dispose()
    {
        Stop();
        GC.SuppressFinalize(this);
    }
}

public class TunnelStatus
{
    public bool IsRunning { get; set; }
    public TunnelType Type { get; set; }
    public string? Url { get; set; }
    public string? PublicIp { get; set; }
}
