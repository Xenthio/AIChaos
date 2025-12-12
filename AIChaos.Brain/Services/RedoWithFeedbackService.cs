using AIChaos.Brain.Models;

namespace AIChaos.Brain.Services;

/// <summary>
/// Service for handling command redos with feedback.
/// First redo per user is free, subsequent redos cost credits.
/// </summary>
public class RedoService
{
    private readonly AccountService _accountService;
    private readonly CommandQueueService _commandQueue;
    private readonly AiCodeGeneratorService _codeGenerator;
    private readonly ILogger<RedoService> _logger;

    public RedoService(
        AccountService accountService,
        CommandQueueService commandQueue,
        AiCodeGeneratorService codeGenerator,
        ILogger<RedoService> logger)
    {
        _accountService = accountService;
        _commandQueue = commandQueue;
        _codeGenerator = codeGenerator;
        _logger = logger;
    }

    /// <summary>
    /// Checks if a user's next redo would be free.
    /// </summary>
    public bool IsNextRedoFree(string accountId)
    {
        var account = _accountService.GetAccountById(accountId);
        if (account == null) return false;
        
        return account.RedoCount == 0;
    }

    /// <summary>
    /// Gets the cost for a user's next redo.
    /// </summary>
    public decimal GetRedoCost(string accountId)
    {
        return IsNextRedoFree(accountId) ? 0 : Constants.Redo.RedoCost;
    }

    /// <summary>
    /// Requests a redo for a command with user feedback explaining the failure.
    /// </summary>
    public async Task<RedoResponse> RequestRedoAsync(
        string accountId, 
        int commandId, 
        string feedback,
        bool isSingleUserMode = false)
    {
        var account = _accountService.GetAccountById(accountId);
        if (account == null)
        {
            return new RedoResponse
            {
                Status = "error",
                Message = "Account not found",
                NewBalance = 0
            };
        }

        var originalCommand = _commandQueue.GetCommand(commandId);
        if (originalCommand == null)
        {
            return new RedoResponse
            {
                Status = "error",
                Message = "Original command not found",
                NewBalance = account.CreditBalance
            };
        }

        // Check if user owns this command
        if (originalCommand.UserId != accountId)
        {
            return new RedoResponse
            {
                Status = "error",
                Message = "You can only redo your own commands",
                NewBalance = account.CreditBalance
            };
        }

        // Determine if this redo is free
        var isFree = account.RedoCount == 0;
        var cost = isFree ? 0 : Constants.Redo.RedoCost;

        // Check credits if not free and not single user mode
        if (!isSingleUserMode && !isFree && account.CreditBalance < cost)
        {
            return new RedoResponse
            {
                Status = "error",
                Message = $"Insufficient credits. Redo costs ${cost:F2}. Your balance: ${account.CreditBalance:F2}",
                NewBalance = account.CreditBalance,
                WasFree = false
            };
        }

        // Deduct credits if not free and not single user mode
        if (!isSingleUserMode && !isFree)
        {
            if (!_accountService.DeductCredits(accountId, cost))
            {
                return new RedoResponse
                {
                    Status = "error",
                    Message = "Failed to deduct credits",
                    NewBalance = account.CreditBalance,
                    WasFree = false
                };
            }
        }

        // Increment redo count
        account.RedoCount++;

        // Generate new code with feedback context
        var feedbackPrompt = $"""
            REDO REQUEST for previous command.
            
            Original request: {originalCommand.UserPrompt}
            
            User feedback about what went wrong:
            {feedback}
            
            Please generate improved code that addresses the user's feedback.
            """;

        try
        {
            var (executionCode, undoCode, needsModeration, moderationReason) = 
                await _codeGenerator.GenerateCodeAsync(feedbackPrompt);

            // Create new command entry as a redo
            var newCommand = _commandQueue.AddCommand(
                originalCommand.UserPrompt,
                executionCode,
                undoCode,
                originalCommand.Source,
                originalCommand.Author,
                originalCommand.ImageContext,
                accountId,
                $"[REDO] Based on feedback: {feedback}"
            );

            // Mark as redo
            newCommand.IsRedo = true;
            newCommand.OriginalCommandId = commandId;
            newCommand.RedoFeedback = feedback;

            var updatedAccount = _accountService.GetAccountById(accountId);
            
            _logger.LogInformation(
                "[REDO] User {Username} requested redo for command #{OriginalId}. New command #{NewId}. Free: {IsFree}", 
                account.Username, commandId, newCommand.Id, isFree);

            return new RedoResponse
            {
                Status = "success",
                Message = isFree 
                    ? "Free redo submitted! Your next redo will cost credits." 
                    : $"Redo submitted! ${cost:F2} deducted.",
                NewCommandId = newCommand.Id,
                NewBalance = updatedAccount?.CreditBalance ?? account.CreditBalance,
                WasFree = isFree
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[REDO] Failed to generate redo code for command #{CommandId}", commandId);
            
            // Refund credits if we already deducted them
            if (!isSingleUserMode && !isFree)
            {
                _accountService.AddCredits(accountId, cost);
            }
            
            return new RedoResponse
            {
                Status = "error",
                Message = "Failed to generate redo code. Credits have been refunded.",
                NewBalance = account.CreditBalance + (isFree ? 0 : cost),
                WasFree = isFree
            };
        }
    }
}
