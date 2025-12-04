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
    private readonly AccountService _accountService;
    private readonly ILogger<RefundService> _logger;
    private readonly ConcurrentDictionary<string, RefundRequest> _requests = new();
    private readonly HashSet<int> _refundedCommandIds = new();
    private readonly object _lock = new();

    public RefundService(AccountService accountService, ILogger<RefundService> logger)
    {
        _accountService = accountService;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new refund request.
    /// </summary>
    public RefundRequest? CreateRequest(string userId, string displayName, int commandId, string prompt, string reason, decimal amount)
    {
        lock (_lock)
        {
            // Check if this command has already been refunded
            if (_refundedCommandIds.Contains(commandId))
            {
                _logger.LogWarning("[REFUND] Duplicate refund request rejected for command #{CommandId} by {User}", commandId, displayName);
                return null;
            }

            // Check if there's already a pending request for this command
            var existingPending = _requests.Values
                .FirstOrDefault(r => r.CommandId == commandId && r.Status == RefundStatus.Pending);
            
            if (existingPending != null)
            {
                _logger.LogWarning("[REFUND] Pending refund request already exists for command #{CommandId} by {User}", commandId, displayName);
                return null;
            }

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
        lock (_lock)
        {
            if (_requests.TryGetValue(requestId, out var request) && request.Status == RefundStatus.Pending)
            {
                // Mark the command as refunded to prevent duplicate refunds
                _refundedCommandIds.Add(request.CommandId);
                
                request.Status = RefundStatus.Approved;
                _accountService.AddCredits(request.UserId, request.Amount);
                _logger.LogInformation("[REFUND] Approved request {Id} for {User}, command #{CommandId}", 
                    requestId, request.UserDisplayName, request.CommandId);
                return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Rejects a refund request.
    /// </summary>
    public bool RejectRefund(string requestId)
    {
        lock (_lock)
        {
            if (_requests.TryGetValue(requestId, out var request) && request.Status == RefundStatus.Pending)
            {
                // Mark the command as refunded (denied) to prevent re-requests
                _refundedCommandIds.Add(request.CommandId);
                
                request.Status = RefundStatus.Rejected;
                _logger.LogInformation("[REFUND] Rejected request {Id} for {User}, command #{CommandId}", 
                    requestId, request.UserDisplayName, request.CommandId);
                return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Checks if a command has already been refunded or has a pending refund request.
    /// </summary>
    public bool CanRequestRefund(int commandId)
    {
        lock (_lock)
        {
            // Check if already refunded
            if (_refundedCommandIds.Contains(commandId))
            {
                return false;
            }

            // Check if there's a pending request
            return !_requests.Values.Any(r => r.CommandId == commandId && r.Status == RefundStatus.Pending);
        }
    }
}
