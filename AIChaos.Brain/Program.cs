using AIChaos.Brain.Models;
using AIChaos.Brain.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();
builder.Services.AddHttpClient();

// Configure settings
builder.Services.Configure<AppSettings>(builder.Configuration.GetSection("AIChaos"));

// Register services as singletons
builder.Services.AddSingleton<SettingsService>();
builder.Services.AddSingleton<CommandQueueService>();
builder.Services.AddSingleton<AiCodeGeneratorService>();
builder.Services.AddSingleton<InteractiveAiService>();
builder.Services.AddSingleton<TwitchService>();
builder.Services.AddSingleton<YouTubeService>();
builder.Services.AddSingleton<TunnelService>();
builder.Services.AddSingleton<ImageModerationService>();

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
app.UseDefaultFiles();
app.UseStaticFiles();

// Map routes for separate pages
app.MapGet("/setup", async context =>
{
    context.Response.ContentType = "text/html";
    await context.Response.SendFileAsync(Path.Combine(app.Environment.WebRootPath, "setup.html"));
});

app.MapGet("/history", async context =>
{
    context.Response.ContentType = "text/html";
    await context.Response.SendFileAsync(Path.Combine(app.Environment.WebRootPath, "history.html"));
});

app.MapGet("/moderation", async context =>
{
    context.Response.ContentType = "text/html";
    await context.Response.SendFileAsync(Path.Combine(app.Environment.WebRootPath, "moderation.html"));
});

app.MapControllers();

// Get the settings service to trigger initialization and show moderation password
var settingsService = app.Services.GetRequiredService<SettingsService>();

Console.WriteLine("========================================");
Console.WriteLine("  AI Chaos Brain - C# Edition");
Console.WriteLine("========================================");
Console.WriteLine($"  Control Panel: http://localhost:5000/");
Console.WriteLine("  Setup: http://localhost:5000/setup");
Console.WriteLine("  History: http://localhost:5000/history");
Console.WriteLine("  Moderation: http://localhost:5000/moderation");
Console.WriteLine("========================================");
Console.WriteLine($"  MODERATION PASSWORD: {settingsService.ModerationPassword}");
Console.WriteLine("  (Password changes each session)");
Console.WriteLine("========================================");

app.Run("http://0.0.0.0:5000");
