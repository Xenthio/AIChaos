using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using AIChaos.Brain.Models;

namespace AIChaos.Brain.Components.Shared;

/// <summary>
/// Base class for Chaos components providing common functionality.
/// </summary>
public abstract class ChaosComponentBase : ComponentBase, IDisposable
{
    private bool _disposed;
    private readonly List<IDisposable> _disposables = new();
    private readonly SemaphoreSlim _messageSemaphore = new(1, 1);
    
    /// <summary>
    /// Shows a temporary status message that auto-dismisses.
    /// Thread-safe with semaphore to prevent race conditions.
    /// </summary>
    protected async Task ShowTemporaryMessageAsync(
        Action<string, string> setMessage, 
        string message, 
        string type, 
        int durationMs = Constants.MessageDurations.Short)
    {
        await _messageSemaphore.WaitAsync();
        try
        {
            setMessage(message, type);
            StateHasChanged();
            
            await Task.Delay(durationMs);
            
            setMessage(string.Empty, type);
            StateHasChanged();
        }
        finally
        {
            _messageSemaphore.Release();
        }
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
        
        _messageSemaphore?.Dispose();
        
        foreach (var disposable in _disposables)
        {
            try
            {
                disposable?.Dispose();
            }
            catch (ObjectDisposedException)
            {
                // Expected when component is being torn down
            }
            catch (Exception ex)
            {
                // Log disposal errors in development
                System.Diagnostics.Debug.WriteLine($"Error disposing resource: {ex.Message}");
            }
        }
        
        _disposables.Clear();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
