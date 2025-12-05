using AIChaos.Brain.Models;
using AIChaos.Brain.Services;
using AIChaos.Brain.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

// Configure forwarded headers for reverse proxy support
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | 
                               ForwardedHeaders.XForwardedProto | 
                               ForwardedHeaders.XForwardedHost;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// Configure HttpClient with base address for Blazor components
builder.Services.AddHttpClient();
builder.Services.AddTransient(sp =>
{
    var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
    var httpClient = httpClientFactory.CreateClient();
    httpClient.BaseAddress = new Uri("http://localhost:5000/");
    return httpClient;
});

// Add Blazor Server services
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Configure settings
builder.Services.Configure<AppSettings>(builder.Configuration.GetSection("AIChaos"));

// Register services as singletons
builder.Services.AddSingleton<SettingsService>();
builder.Services.AddSingleton<CommandQueueService>();
builder.Services.AddSingleton<QueueSlotService>();
builder.Services.AddSingleton<AiCodeGeneratorService>();
builder.Services.AddSingleton<AccountService>();
builder.Services.AddSingleton<RefundService>();
builder.Services.AddSingleton<CurrencyConversionService>();
builder.Services.AddSingleton<TwitchService>();
builder.Services.AddSingleton<YouTubeService>();
builder.Services.AddSingleton<TunnelService>();
builder.Services.AddSingleton<ImageModerationService>();
builder.Services.AddSingleton<TestClientService>();
builder.Services.AddSingleton<AgenticGameService>();

// Configure CORS for local development
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Handle X-Forwarded-Prefix from nginx for subdirectory deployment
// This MUST be before UseForwardedHeaders and other middleware
app.Use(async (context, next) =>
{
    var forwardedPrefix = context.Request.Headers["X-Forwarded-Prefix"].FirstOrDefault();
    if (!string.IsNullOrEmpty(forwardedPrefix))
    {
        //Console.WriteLine($"[DEBUG] Received X-Forwarded-Prefix: {forwardedPrefix}");
        context.Request.PathBase = forwardedPrefix;
        
        // Also need to strip the prefix from the path if nginx didn't
        if (context.Request.Path.StartsWithSegments(forwardedPrefix, out var remainder))
        {
            context.Request.Path = remainder;
        }
        
        //Console.WriteLine($"[DEBUG] PathBase: {context.Request.PathBase}, Path: {context.Request.Path}");
    }
    await next();
});

// Use forwarded headers
app.UseForwardedHeaders();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors();
app.UseStaticFiles();
app.UseAntiforgery();

// Map Blazor components
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapControllers();

// Get the settings service to trigger initialization and show moderation password
var settingsService = app.Services.GetRequiredService<SettingsService>();

Console.WriteLine("========================================");
Console.WriteLine("  Chaos Brain - C# Edition");
Console.WriteLine("========================================");
Console.WriteLine($"  Viewer: http://localhost:5000/");
Console.WriteLine("  Dashboard: http://localhost:5000/dashboard");
Console.WriteLine("  Setup: http://localhost:5000/dashboard/setup");
Console.WriteLine("  History: http://localhost:5000/dashboard/history");
Console.WriteLine("  Moderation: http://localhost:5000/dashboard/moderation");
Console.WriteLine("========================================");
Console.WriteLine($"  MODERATION PASSWORD: {settingsService.ModerationPassword} (OBSOLETE)");
Console.WriteLine("  (Password changes each session)");
Console.WriteLine("========================================");

// Register shutdown handler to stop tunnels when server closes
var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
var tunnelService = app.Services.GetRequiredService<TunnelService>();
lifetime.ApplicationStopping.Register(() =>
{
    Console.WriteLine("Server shutting down...");
    if (tunnelService.IsRunning)
    {
        Console.WriteLine("Stopping tunnel...");
        tunnelService.Stop();
        Console.WriteLine("Tunnel stopped.");
    }
});

app.Run("http://0.0.0.0:5000");
