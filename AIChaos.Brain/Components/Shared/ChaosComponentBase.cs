using Microsoft.AspNetCore.Components;
using AIChaos.Brain.Models;

namespace AIChaos.Brain.Components.Shared;

/// <summary>
/// Base class for Chaos components providing common functionality.
/// </summary>
public abstract class ChaosComponentBase : ComponentBase, IDisposable
{
    private bool _disposed;
    private readonly List<IDisposable> _disposables = new();
    
    /// <summary>
    /// Shows a temporary status message that auto-dismisses.
    /// </summary>
    protected async Task ShowTemporaryMessageAsync(
        Action<string, string> setMessage, 
        string message, 
        string type, 
        int durationMs = Constants.MessageDurations.Short)
    {
        setMessage(message, type);
        StateHasChanged();
        
        await Task.Delay(durationMs);
        
        setMessage(string.Empty, type);
        StateHasChanged();
    }
    
    /// <summary>
    /// Registers a disposable resource to be cleaned up when component is disposed.
    /// </summary>
    protected void RegisterDisposable(IDisposable disposable)
    {
        if (disposable != null)
        {
            _disposables.Add(disposable);
        }
    }
    
    /// <summary>
    /// Safe async operation wrapper with error handling.
    /// </summary>
    protected async Task<(bool Success, string? ErrorMessage)> TryExecuteAsync(
        Func<Task> operation, 
        string? errorPrefix = null)
    {
        try
        {
            await operation();
            return (true, null);
        }
        catch (Exception ex)
        {
            var message = errorPrefix != null 
                ? $"{errorPrefix}: {ex.Message}" 
                : ex.Message;
            return (false, message);
        }
    }
    
    /// <summary>
    /// Disposes component resources.
    /// </summary>
    public virtual void Dispose()
    {
        if (_disposed) return;
        
        foreach (var disposable in _disposables)
        {
            try
            {
                disposable?.Dispose();
            }
            catch
            {
                // Ignore disposal errors
            }
        }
        
        _disposables.Clear();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
