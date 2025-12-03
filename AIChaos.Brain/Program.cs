using AIChaos.Brain.Models;
using AIChaos.Brain.Services;
using AIChaos.Brain.Components;
using Microsoft.AspNetCore.Components;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

// Configure HttpClient with base address for Blazor components
// Note: In Blazor Server, components run on the server and make HTTP calls to the same server
// We set a base address so relative URLs like "/api/account/login" work
builder.Services.AddHttpClient();
builder.Services.AddTransient(sp =>
{
    // Get the HttpClient from the factory and configure it
    var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
    var httpClient = httpClientFactory.CreateClient();
    
    // Set base address to localhost - this works because Blazor Server runs on the same machine
    // In production, this should match your deployment configuration
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
builder.Services.AddSingleton<AiCodeGeneratorService>();
builder.Services.AddSingleton<AccountService>();
builder.Services.AddSingleton<RefundService>();
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
Console.WriteLine("  AI Chaos Brain - C# Edition");
Console.WriteLine("========================================");
Console.WriteLine($"  Viewer: http://localhost:5000/");
Console.WriteLine("  Dashboard: http://localhost:5000/dashboard");
Console.WriteLine("  Setup: http://localhost:5000/setup");
Console.WriteLine("  History: http://localhost:5000/history");
Console.WriteLine("  Moderation: http://localhost:5000/moderation");
Console.WriteLine("========================================");
Console.WriteLine($"  MODERATION PASSWORD: {settingsService.ModerationPassword}");
Console.WriteLine("  (Password changes each session)");
Console.WriteLine("========================================");

app.Run("http://0.0.0.0:5000");
