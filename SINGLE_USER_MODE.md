# Single User Mode - Implementation Summary

## Overview

AIChaos now supports a **Single User Mode** that allows the application to run without requiring user login or credit management. This mode is **enabled by default** to provide the simplest possible setup experience.

## Key Features

### Single User Mode (Default)
When `SingleUserMode` is enabled (default):
- ✅ **No login required** - Users can submit commands immediately
- ✅ **Unlimited submissions** - No credit system or balance checks
- ✅ **No rate limiting** - Commands can be submitted without waiting
- ✅ **Admin access preserved** - Dashboard login still works for administrators
- ✅ **Works offline** - Doesn't require YouTube stream to be active

### Multi-User Mode
When `SingleUserMode` is disabled:
- 🔐 Login required for submissions
- 💰 Credit system enforced ($1 per command)
- ⏱️ Rate limiting applied (20 seconds between commands)
- 📺 Requires YouTube stream to be active for viewer submissions

## Configuration

### Enabling/Disabling Single User Mode

Edit `appsettings.json`:

```json
{
  "AIChaos": {
    "General": {
      "SingleUserMode": true
    }
  }
}
```

Set to `false` to enable the full credit and authentication system.

## Technical Implementation

### Anonymous User Account
- Single user mode creates an anonymous account automatically
- Account ID: `anonymous-default-user`
- Username: `anonymous`
- Credit balance: Unlimited (`decimal.MaxValue`)
- Role: Regular User (cannot access dashboard)

### Admin Authentication
Even in single user mode, administrators can:
1. Navigate to `/dashboard`
2. Create an account (first account becomes admin automatically)
3. Log in using the form on the dashboard
4. Access all admin/moderator features

### Code Changes

#### Core Services
- **AccountService**: Added `GetOrCreateAnonymousAccount()` method
- **AccountService**: Modified submission methods to skip credit/rate limit checks in single user mode
- **AppSettings**: Added `GeneralSettings.SingleUserMode` property

#### UI Components
- **MainLayout**: Automatically uses anonymous account when not logged in (single user mode)
- **MainLayout**: Hides login UI for anonymous users in single user mode
- **Index**: Removes credit-related messaging in single user mode
- **Dashboard**: Provides login form for admin access

## Security Considerations

### Single User Mode
- ✅ **Safe for local use** - Perfect for single-player or local testing
- ⚠️ **Not recommended for public servers** - Anyone can submit unlimited commands
- ℹ️ **Dashboard protected** - Admin features still require authentication

### Multi-User Mode
- ✅ **Safe for public use** - Credit system prevents spam
- ✅ **Rate limiting** - Prevents abuse
- ✅ **Authentication required** - Only authenticated users can submit

## Migration Path

### From Multi-User to Single User
No migration needed. Simply set `SingleUserMode: true` and restart.

### From Single User to Multi-User
1. Set `SingleUserMode: false` in `appsettings.json`
2. Configure YouTube OAuth (optional)
3. Set up payment processing for credits (if using YouTube Super Chat)
4. Restart the application

## Testing

All functionality is covered by automated tests:
- Anonymous account creation
- Credit bypass in single user mode
- Admin authentication preservation
- 79 tests total, all passing
- 0 security vulnerabilities (CodeQL scan)

## Use Cases

### Single User Mode (Default)
- 🎮 **Local testing** - Test ideas without setup
- 🏠 **Personal use** - Play with friends locally
- 🔬 **Development** - Develop and test new features
- 📺 **Small streams** - Run without payment integration

### Multi-User Mode
- 🌐 **Public streams** - Monetize with YouTube Super Chat
- 👥 **Large audience** - Manage viewer submissions with credits
- 💼 **Commercial use** - Charge for command submissions
- 🛡️ **Spam prevention** - Prevent abuse with rate limiting

## Best Practices

1. **Use Single User Mode for**:
   - Local development and testing
   - Personal/private use
   - Small streams with trusted audience

2. **Use Multi-User Mode for**:
   - Public streams with unknown audience
   - Monetized content
   - When spam prevention is needed

3. **Always**:
   - Set an admin password on first use
   - Keep the dashboard credentials secure
   - Monitor the command history for abuse

## Future Enhancements

Potential additions (not yet implemented):
- IP-based rate limiting in single user mode
- Configurable anonymous user display name
- Optional password protection for single user mode
- Command history cleanup/limits
