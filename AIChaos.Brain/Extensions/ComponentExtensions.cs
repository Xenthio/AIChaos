using Microsoft.AspNetCore.Components;

namespace AIChaos.Brain.Extensions;

/// <summary>
/// Extension methods for Blazor components - helper methods only, no InvokeAsync needed.
/// </summary>
public static class ComponentExtensions
{
    /// <summary>
    /// Creates a task that shows a temporary message and auto-dismisses.
    /// Use with fire-and-forget pattern: _ = ShowTemporaryMessageAsync(...);
    /// </summary>
    public static Task ShowTemporaryMessageAsync(
        Action setMessage,
        Action clearMessage,
        int durationMs = 3000)
    {
        return Task.Run(async () =>
        {
            setMessage();
            await Task.Delay(durationMs);
            clearMessage();
        });
    }
}
