using System.Collections.Concurrent;
using AIChaos.Brain.Models;

namespace AIChaos.Brain.Services;

public class RefundRequest
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string UserId { get; set; } = "";
    public string UserDisplayName { get; set; } = "";
    public int CommandId { get; set; }
    public string Prompt { get; set; } = "";
    public string Reason { get; set; } = "";
    public decimal Amount { get; set; }
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public RefundStatus Status { get; set; } = RefundStatus.Pending;
}

public enum RefundStatus
{
    Pending,
    Approved,
    Rejected
}

/// <summary>
/// Service for managing refund requests.
/// </summary>
public class RefundService
{
    private readonly UserService _userService;
    private readonly ILogger<RefundService> _logger;
    private readonly ConcurrentDictionary<string, RefundRequest> _requests = new();

    public RefundService(UserService userService, ILogger<RefundService> logger)
    {
        _userService = userService;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new refund request.
    /// </summary>
    public RefundRequest CreateRequest(string userId, string displayName, int commandId, string prompt, string reason, decimal amount)
    {
        var request = new RefundRequest
        {
            UserId = userId,
            UserDisplayName = displayName,
            CommandId = commandId,
            Prompt = prompt,
            Reason = reason,
            Amount = amount
        };

        _requests.TryAdd(request.Id, request);
        _logger.LogInformation("[REFUND] New request from {User}: {Reason} (${Amount})", displayName, reason, amount);

        return request;
    }

    /// <summary>
    /// Gets all pending refund requests.
    /// </summary>
    public List<RefundRequest> GetPendingRequests()
    {
        return _requests.Values
            .Where(r => r.Status == RefundStatus.Pending)
            .OrderByDescending(r => r.RequestedAt)
            .ToList();
    }

    /// <summary>
    /// Approves a refund request and returns credits to the user.
    /// </summary>
    public bool ApproveRefund(string requestId)
    {
        if (_requests.TryGetValue(requestId, out var request) && request.Status == RefundStatus.Pending)
        {
            request.Status = RefundStatus.Approved;
            _userService.AddCredits(request.UserId, request.Amount, request.UserDisplayName);
            _logger.LogInformation("[REFUND] Approved request {Id} for {User}", requestId, request.UserDisplayName);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Rejects a refund request.
    /// </summary>
    public bool RejectRefund(string requestId)
    {
        if (_requests.TryGetValue(requestId, out var request) && request.Status == RefundStatus.Pending)
        {
            request.Status = RefundStatus.Rejected;
            _logger.LogInformation("[REFUND] Rejected request {Id} for {User}", requestId, request.UserDisplayName);
            return true;
        }
        return false;
    }
}
