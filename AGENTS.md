# 🤖 AGENTS.md - Guide for AI Agents Working on AIChaos

This document provides AI agents with essential context about the AIChaos project, its architecture, conventions, and important considerations when making changes.

---

## 📋 Project Overview

**AIChaos** is an interactive streaming platform that allows viewers to control a Garry's Mod game through natural language "Ideas". The system uses LLM-powered code generation to turn viewer requests into actual game commands.

### Core Concept
1. Viewer submits an "Idea" (e.g., "make everyone tiny")
2. Brain server generates Lua code using LLM
3. Optional moderation review
4. Code sent to Garry's Mod addon
5. Code executes in game

### Key Features
- **Single User Mode** (default): No auth/credits required for local testing
- **Multi-User Mode**: Full economy with $1 per Idea, YouTube Super Chat integration
- **Slot-based Queue**: Dynamic pacing (3-10 concurrent slots based on demand)
- **Moderation System**: Admin/moderator review of code and images
- **Interactive Mode**: Real-time streaming with viewers

---

## 🏗️ Architecture

### Technology Stack
- **Backend**: ASP.NET Core 9.0 (C#)
- **Frontend**: Blazor Server (interactive server-side rendering)
- **Game Integration**: Garry's Mod Lua addon
- **LLM**: OpenRouter API (supports multiple models)
- **Database**: JSON file-based storage (accounts.json, settings.json)

### Project Structure

```
AIChaos/
├── AIChaos.Brain/              # Main server application
│   ├── Controllers/            # API endpoints
│   │   ├── AccountController.cs     # User auth, credits, YouTube linking
│   │   ├── ChaosController.cs       # GMod polling, command submission
│   │   ├── ModerationController.cs  # Code/image moderation
│   │   └── StreamPanelController.cs # Dashboard state APIs
│   ├── Services/               # Business logic (all singletons)
│   │   ├── AccountService.cs        # User management, credits (1,177 lines)
│   │   ├── AgenticGameService.cs    # Interactive mode handler (1,091 lines)
│   │   ├── AiCodeGeneratorService.cs # LLM code generation
│   │   ├── CodeModerationService.cs  # Code safety checks
│   │   ├── CommandQueueService.cs    # Command queue management
│   │   ├── CurrencyConversionService.cs # USD conversion
│   │   ├── ImageModerationService.cs # Image URL validation
│   │   ├── QueueSlotService.cs      # Dynamic slot management
│   │   ├── RefundService.cs         # Refund request handling
│   │   ├── SettingsService.cs       # App configuration
│   │   ├── TestClientService.cs     # Test mode for development
│   │   ├── TunnelService.cs         # ngrok/bore tunnel management
│   │   ├── TwitchService.cs         # Twitch chat integration
│   │   └── YouTubeService.cs        # YouTube Super Chat integration
│   ├── Components/             # Blazor components
│   │   ├── Pages/              # Full pages (Dashboard, Index, Setup)
│   │   ├── Shared/             # Reusable components
│   │   │   ├── Panels/         # Modular dashboard panels (NEW)
│   │   │   │   ├── QueueControlPanel.razor
│   │   │   │   ├── StreamSettingsPanel.razor
│   │   │   │   ├── ImageModerationPanel.razor
│   │   │   │   ├── RefundRequestsPanel.razor
│   │   │   │   ├── CodeModerationPanel.razor
│   │   │   │   └── GlobalHistoryPanel.razor
│   │   │   ├── ChaosComponentBase.cs # Base class with common utilities
│   │   │   └── [Other shared components]
│   │   └── Layout/             # Layout components
│   ├── Models/                 # Data models
│   │   ├── Account.cs          # User account model
│   │   ├── ApiModels.cs        # Consolidated request/response DTOs
│   │   ├── AppSettings.cs      # Configuration model
│   │   ├── Constants.cs        # Centralized constants (NEW)
│   │   ├── PendingChannelCredits.cs
│   │   └── ServiceResult.cs    # Standardized result wrapper
│   ├── Helpers/                # Utility classes (NEW)
│   │   ├── CommandStatusHelper.cs # Status display helpers
│   │   └── SafetyHelper.cs        # URL filtering, dangerous patterns
│   ├── Extensions/             # Extension methods (NEW)
│   │   └── ComponentExtensions.cs
│   └── wwwroot/                # Static files (CSS, JS)
├── AIChaos.Brain.Tests/        # Unit tests (79 tests)
├── lua/                        # Garry's Mod addon
│   └── autorun/
│       └── chaos_controller.lua # GMod polling/execution
└── gamemodes/                  # Game mode implementations

```

---

## 🎯 Key Design Patterns & Conventions

### 1. Service Architecture
- **All services are singletons** - Registered in `Program.cs` with `AddSingleton<T>()`
- **No interfaces** - Services are directly injected (tight coupling by design)
- **Thread safety**: Services use manual `lock` statements or concurrent collections
- **Large services**: AccountService (1,177 lines) and AgenticGameService (1,091 lines) are candidates for splitting but not done yet

### 2. Component Patterns
- **Base class**: `ChaosComponentBase` provides:
  - Thread-safe `ShowTemporaryMessageAsync()` with `SemaphoreSlim`
  - `RegisterDisposable()` for resource management
  - `TryExecuteAsync()` for standardized error handling
- **Panel components**: Use `EventCallback` for parent communication
- **IDisposable**: All panels with timers must implement proper cleanup
- **Async patterns**: Always use `Task.Run(() => { await Task.Delay(...); await InvokeAsync(...); })` for Blazor sync context

### 3. Code Organization
- **Constants**: Use `Constants` class with nested classes (Sources, Authors, AlertTypes, MessageDurations, UserIds)
- **Helpers**: Centralized utilities in `Helpers/` directory (CommandStatusHelper, SafetyHelper)
- **Models**: Consolidate request/response DTOs in `Models/ApiModels.cs`
- **CSS**: Shared styles in `wwwroot/css/style.css`, component-specific styles inline only when necessary

### 4. Naming Conventions
- **Controllers**: End with `Controller` (e.g., `ChaosController`)
- **Services**: End with `Service` (e.g., `SettingsService`)
- **Models**: Descriptive names without suffixes (e.g., `Account`, `Command`)
- **API endpoints**: RESTful conventions (`/api/chaos/submit`, `/api/account/register`)

---

## 🚨 Critical Things to Know

### Security Considerations
1. **Password hashing**: Uses `Rfc2898DeriveBytes.Pbkdf2()` (not deprecated constructor)
2. **Admin protection**: Dashboard requires authentication, but submission page is public
3. **Code safety**: `SafetyHelper.ContainsDangerousPatterns()` filters malicious Lua patterns
4. **URL validation**: All image URLs validated through `SafetyHelper.FilterUrls()`
5. **OAuth**: YouTube integration exists but may have issues (user-reported bugs)

### Performance Gotchas
1. **File I/O**: accounts.json and settings.json are loaded on every request (no caching)
2. **Polling**: GMod addon polls `/api/chaos/poll` every 2 seconds
3. **Queue slots**: Dynamic (3-10) based on queue depth, 25-second timer per slot
4. **Concurrent execution**: Multiple commands can execute simultaneously in game

### Known Issues (As of Recent Comments)
1. ⚠️ **Moderation bypass**: Users can bypass pending image moderation by hitting retry
2. ⚠️ **Credit deduction**: Retry in user history doesn't cost credits (should charge)
3. ⚠️ **Interactive mode**: Not subject to post-code generation filters
4. ⚠️ **Google OAuth**: User-side OAuth broken (currently hidden in UI)

### Anti-Patterns Fixed in Recent Cleanup
- ✅ `Task.Delay().ContinueWith()` → Use `Task.Run()` + `InvokeAsync()`
- ✅ `async void` → Always return `Task` with proper error handling
- ✅ `.GetAwaiter().GetResult()` → Use proper `await`
- ✅ Empty catch blocks → Add logging
- ✅ Magic numbers → Use `Constants.MessageDurations.*`

---

## 🧪 Testing

### Test Suite
- **Location**: `AIChaos.Brain.Tests/`
- **Framework**: xUnit
- **Coverage**: 79 tests covering services and models
- **Command**: `dotnet test` from solution root

### Test Categories
- **Account tests**: Registration, login, credits
- **Queue tests**: Command queueing, slot management
- **Moderation tests**: Code safety, image validation
- **Service tests**: Currency conversion, settings management

### Testing Gotchas
- Tests use in-memory state (no actual JSON files)
- Some tests require mocked `HttpContext` for session handling
- YouTube/Twitch tests may need API mocking

---

## 📝 Making Changes

### Before You Start
1. **Read existing code** in the area you're modifying
2. **Check for duplicates** - Use helper classes instead of repeating logic
3. **Review recent commits** - Understand recent refactoring patterns
4. **Run tests** - Ensure baseline passes before changes

### Code Style
- **Indentation**: 4 spaces (C#), 2 spaces (Razor)
- **Braces**: Always use braces, even for single-line blocks
- **Comments**: Only when explaining complex logic, not obvious code
- **Logging**: Use `ILogger<T>` with structured logging patterns
- **Error handling**: Prefer `ServiceResult<T>` wrapper over exceptions

### Component Changes
- **Panel splitting**: See `Components/Shared/Panels/README.md` for patterns
- **EventCallback**: Use for parent-child communication, not direct service calls
- **Auto-refresh**: Use `Task.Run()` with proper `InvokeAsync()` for UI updates
- **Disposal**: Always clean up timers/resources in `Dispose()`

### Service Changes
- **Thread safety**: Use locks when modifying shared state
- **Singleton state**: Remember services are shared across all requests
- **File I/O**: Always use try-catch for JSON operations
- **Logging**: Log important state changes and errors

### API Changes
- **Breaking changes**: Avoid changing existing endpoint contracts
- **New endpoints**: Follow RESTful conventions
- **Authorization**: Use `[RequireAdminAuth]` or `[RequireModeratorAuth]` attributes
- **Validation**: Validate inputs, return proper HTTP status codes

---

## 🔍 Common Tasks

### Adding a New Feature
1. **Service layer**: Add business logic to appropriate service (or create new service)
2. **Controller**: Add API endpoint if needed
3. **Component**: Create Blazor component for UI
4. **Tests**: Add unit tests for new logic
5. **Constants**: Add any magic strings/numbers to `Constants`

### Fixing a Bug
1. **Reproduce**: Understand the issue in context
2. **Tests**: Write a failing test first (if possible)
3. **Fix**: Minimal changes to address the root cause
4. **Verify**: Run tests and manual verification
5. **Document**: Update comments/docs if behavior changes

### Refactoring
1. **Tests first**: Ensure tests pass before refactoring
2. **Small steps**: Make incremental changes
3. **Commit often**: Each logical change should be a commit
4. **Verify after each step**: Run tests frequently
5. **Document patterns**: Update AGENTS.md if introducing new patterns

---

## 📚 Important Files to Reference

### Documentation
- `README.md` - User-facing installation and usage guide
- `SINGLE_USER_MODE.md` - Default mode explanation
- `YOUTUBE_SETUP.md` - YouTube OAuth setup
- `TUNNEL_COMPARISON.md` - ngrok vs bore vs others
- `COMMAND_HISTORY_GUIDE.md` - History/undo system
- `Components/Shared/Panels/README.md` - Component splitting pattern
- `AGENTS.md` - This file (for AI agents)

### Configuration
- `appsettings.json` - App configuration (Single User Mode toggle, etc.)
- `appsettings.Development.json` - Dev-specific settings
- JSON data files (created at runtime):
  - `accounts.json` - User accounts and balances
  - `settings.json` - App settings and API keys
  - `ngrok_url.txt` / `bore_url.txt` - Tunnel URLs
  - `tunnel_url.txt` - External server URL (GMod addon)

### Entry Points
- `Program.cs` - Application startup, DI configuration
- `Controllers/ChaosController.cs` - Main command submission and polling
- `lua/autorun/chaos_controller.lua` - GMod addon entry point

---

## 🎨 UI/UX Patterns

### Dashboard Structure
- **Stream Control** (default): All-in-one streaming hub
  - Queue control panel
  - Stream settings panel
  - Image moderation panel
  - Refund requests panel
  - Code moderation panel
  - Global history panel
- **Setup**: Configuration and admin settings
- **Commands**: Browse saved payloads
- **History**: Full command history
- **Moderation**: Pending code/image review
- **Users**: User management (Admin only)
- **Testing**: Test client (Admin only)

### Role-Based Access
- **Public**: Can submit Ideas (submission page)
- **Moderators**: Stream Control, Commands, History, Moderation
- **Admins**: Full access including Setup and Users

### Mobile Responsiveness
- Horizontal scrolling tabs on mobile
- Responsive layouts using CSS Grid/Flexbox
- Touch-friendly buttons and controls

---

## 🚀 Development Workflow

### Local Development
```bash
# Build and run
cd AIChaos.Brain
dotnet restore
dotnet run

# Run tests
cd ../AIChaos.Brain.Tests
dotnet test

# Build only
dotnet build
```

### Debugging
- Visual Studio / VS Code debugging fully supported
- Blazor Server allows breakpoints in components
- Use browser dev tools for client-side inspection
- Check console logs in GMod for addon issues

### Hot Reload
- Blazor hot reload works for most component changes
- Service changes require restart
- Static files (CSS/JS) may need hard refresh

---

## 💡 Pro Tips for Agents

1. **Search before creating**: Use `grep` to find existing patterns before creating new ones
2. **Helper classes**: Check `Helpers/` and `Extensions/` before duplicating code
3. **Constants first**: Add to `Constants` class instead of hardcoding strings/numbers
4. **Component base**: Inherit from `ChaosComponentBase` for common utilities
5. **Thread safety**: Services are singletons, always consider thread safety
6. **Test coverage**: Maintain the 79/79 passing tests, add new ones for new features
7. **Minimal changes**: This project values small, focused changes over large rewrites
8. **Documentation**: Update relevant .md files when changing behavior

---

## 🐛 Debugging Common Issues

### Compiler Warnings
- Update to `Rfc2898DeriveBytes.Pbkdf2()` not old constructor
- Use `KnownIPNetworks` not deprecated `KnownNetworks`
- Add null-coalescing operators for nullable references
- Mark intentional fire-and-forget with `_ = Task.Run(...)`

### Runtime Issues
- **File not found**: JSON files created on first run
- **Port in use**: Default is 5000, change in `Program.cs` or `--urls` arg
- **Authentication fails**: Check admin password set in `/dashboard`
- **Commands not executing**: Check GMod console for addon errors

### Blazor Issues
- **State not updating**: Use `InvokeAsync(StateHasChanged)`
- **Component not re-rendering**: Check parameter binding
- **Timer not cleaning up**: Implement `IDisposable` properly
- **Race conditions**: Use `SemaphoreSlim` for async synchronization

---

## 🔮 Future Improvements (Not Yet Done)

These are areas identified for improvement but not yet implemented:

1. **Service interfaces** - Extract interfaces for better testability (14 services)
2. **Large service splitting** - Break down AccountService (1,177 lines) and AgenticGameService (1,091 lines)
3. **Database migration** - Move from JSON files to proper database
4. **Caching layer** - Cache frequently accessed data
5. **Real-time updates** - Use SignalR for live dashboard updates
6. **Audit logging** - Track all admin/moderator actions
7. **Rate limiting** - Implement per-user rate limits
8. **OAuth fixes** - Fix broken Google OAuth flow

---

## 📞 Getting Help

When you encounter issues:
1. Check existing documentation in this file
2. Search commit history for related changes (`git log --grep="keyword"`)
3. Review test files for expected behavior
4. Check console/browser logs for runtime errors
5. Ask the repository owner via GitHub issues

---

**Last Updated**: December 2024 (Post Phase 2 Component Splitting)  
**Maintained By**: AI Agents & Project Contributors  
**Version**: 1.0

---

## 🎯 TL;DR for Quick Reference

- **Stack**: ASP.NET Core 9.0 + Blazor Server + Garry's Mod Lua
- **Services**: All singletons, no interfaces, manual thread safety
- **Components**: Use `ChaosComponentBase`, `EventCallback`, proper `IDisposable`
- **Async**: Always `Task.Run()` + `InvokeAsync()` for Blazor context
- **Constants**: Use `Constants` class, never hardcode strings/numbers
- **Tests**: 79 tests must pass, run with `dotnet test`
- **Code style**: Clean, minimal, well-documented changes
- **Known bugs**: Moderation bypass, credit deduction, interactive mode filtering, OAuth broken
- **Recent work**: Phase 2 component splitting complete (StreamControlContent → 6 panels)
