using AIChaos.Brain.Models;
using AIChaos.Brain.Services;
using AIChaos.Brain.Components;
using AIChaos.Brain.Data;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;

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
    options.KnownIPNetworks.Clear();
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

// Configure Entity Framework Core with SQLite
var dbPath = Path.Combine(AppContext.BaseDirectory, "aichaos.db");
builder.Services.AddDbContext<AIChaosDbContext>(options =>
    options.UseSqlite($"Data Source={dbPath}"));

// Register services as singletons
builder.Services.AddSingleton<LogCaptureService>();
builder.Services.AddSingleton<ISettingsService, SettingsService>();
builder.Services.AddSingleton<SettingsService>(sp => (SettingsService)sp.GetRequiredService<ISettingsService>());
builder.Services.AddSingleton<CommandQueueService>();
builder.Services.AddSingleton<QueueSlotService>();
builder.Services.AddSingleton<AiCodeGeneratorService>();
builder.Services.AddSingleton<AccountService>();
builder.Services.AddSingleton<RefundService>();
builder.Services.AddSingleton<CurrencyConversionService>();
builder.Services.AddSingleton<TwitchService>();
builder.Services.AddSingleton<YouTubeService>();
builder.Services.AddSingleton<TunnelService>();
builder.Services.AddSingleton<PromptModerationService>();
builder.Services.AddSingleton<CodeModerationService>();
builder.Services.AddSingleton<TestClientService>();
builder.Services.AddSingleton<AgenticGameService>();
builder.Services.AddSingleton<CommandConsumptionService>();
builder.Services.AddSingleton<RedoService>();
builder.Services.AddSingleton<IOpenRouterService, OpenRouterService>();
builder.Services.AddSingleton<OpenRouterService>(sp => (OpenRouterService)sp.GetRequiredService<IOpenRouterService>());
builder.Services.AddSingleton<FavouritesService>();

// Configure log capture for admin viewing - use a factory to avoid BuildServiceProvider warning
builder.Services.AddSingleton<ILoggerProvider>(sp => 
    new LogCaptureProvider(sp.GetRequiredService<LogCaptureService>()));

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

// Initialize database (apply migrations)
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AIChaosDbContext>();
    dbContext.Database.Migrate();
}

// Handle X-Forwarded-Prefix from nginx for subdirectory deployment
// This MUST be before UseForwardedHeaders and other middleware
app.Use(async (context, next) =>
{
    var forwardedPrefix = context.Request.Headers["X-Forwarded-Prefix"].FirstOrDefault();
    if (!string.IsNullOrEmpty(forwardedPrefix))
    {
        context.Request.PathBase = forwardedPrefix;
        
        // Also need to strip the prefix from the path if nginx didn't
        if (context.Request.Path.StartsWithSegments(forwardedPrefix, out var remainder))
        {
            context.Request.Path = remainder;
        }
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

// Initialize settings service
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
