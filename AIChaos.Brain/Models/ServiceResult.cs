namespace AIChaos.Brain.Models;

/// <summary>
/// Generic result type for service operations.
/// </summary>
public class ServiceResult
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string? Url { get; set; }
    
    public ServiceResult(bool success, string? message = null, string? url = null)
    {
        Success = success;
        Message = message;
        Url = url;
    }
    
    public static ServiceResult Ok(string? message = null, string? url = null) => 
        new(true, message, url);
    
    public static ServiceResult Fail(string? message = null) => 
        new(false, message);
}

/// <summary>
/// Generic result type for service operations with data.
/// </summary>
public class ServiceResult<T> : ServiceResult
{
    public T? Data { get; set; }
    
    public ServiceResult(bool success, T? data = default, string? message = null) 
        : base(success, message)
    {
        Data = data;
    }
    
    public static ServiceResult<T> Ok(T data, string? message = null) => 
        new(true, data, message);
    
    public static new ServiceResult<T> Fail(string? message = null) => 
        new(false, default, message);
}
