using System.Text.RegularExpressions;
using AIChaos.Brain.Models;

namespace AIChaos.Brain.Helpers;

/// <summary>
/// Helper class for safety and filtering operations across services.
/// </summary>
public static partial class SafetyHelper
{
    /// <summary>
    /// Filters URLs from a message based on safety settings.
    /// </summary>
    public static string FilterUrls(string message, bool isMod, SafetySettings safety)
    {
        if (!safety.BlockUrls || isMod)
        {
            return message;
        }

        var urlPattern = UrlRegex();
        var matches = urlPattern.Matches(message);
        var filtered = message;

        foreach (Match match in matches)
        {
            var url = match.Value;
            try
            {
                var uri = new Uri(url);
                var host = uri.Host.ToLowerInvariant();

                // Allow whitelisted domains
                if (safety.AllowedDomains.Any(pattern =>
                    host.Contains(pattern, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                // Replace URL with placeholder
                filtered = filtered.Replace(url, "[URL_BLOCKED]");
            }
            catch
            {
                // If URL parsing fails, block it anyway
                filtered = filtered.Replace(url, "[URL_BLOCKED]");
            }
        }

        return filtered;
    }

    /// <summary>
    /// Checks if code contains dangerous patterns.
    /// </summary>
    public static bool ContainsDangerousPatterns(string code)
    {
        var dangerousPatterns = new[]
        {
            "changelevel",
            @"RunConsoleCommand.*""map""",
            @"RunConsoleCommand.*'map'",
            @"game\.ConsoleCommand.*map"
        };

        return dangerousPatterns.Any(pattern =>
            Regex.IsMatch(code, pattern, RegexOptions.IgnoreCase));
    }

    [GeneratedRegex(@"https?://[^\s]+", RegexOptions.IgnoreCase)]
    private static partial Regex UrlRegex();
}
