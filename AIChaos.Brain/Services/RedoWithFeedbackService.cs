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
    private readonly PromptModerationService _promptModerationService;
    private readonly CodeModerationService _codeModerationService;
    private readonly ILogger<RedoService> _logger;

    public RedoService(
        AccountService accountService,
        CommandQueueService commandQueue,
        AiCodeGeneratorService codeGenerator,
        PromptModerationService promptModerationService,
        CodeModerationService codeModerationService,
        ILogger<RedoService> logger)
    {
        _accountService = accountService;
        _commandQueue = commandQueue;
        _codeGenerator = codeGenerator;
        _promptModerationService = promptModerationService;
        _codeModerationService = codeModerationService;
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

        // Determine if this redo is free (needed for accurate error responses)
        var isFree = account.RedoCount == 0;
        var cost = isFree ? 0 : Constants.Redo.RedoCost;

        // Security check: Check if feedback contains URLs or external content that needs moderation
        var feedbackNeedsModeration = _promptModerationService.NeedsModeration(feedback);
        if (feedbackNeedsModeration)
        {
            var urls = _promptModerationService.ExtractContentUrls(feedback);
            _logger.LogWarning(
                "[FIX-SECURITY] User {Username} attempted to bypass moderation by including {Count} URL(s) in fix feedback for command #{CommandId}",
                account.Username, urls.Count, commandId);
            
            return new RedoResponse
            {
                Status = "error",
                Message = "Your feedback contains URLs or external links which are not allowed for security reasons. Please describe the issue in your own words without including links.",
                NewBalance = account.CreditBalance,
                WasFree = isFree
            };
        }

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
        if (!isSingleUserMode && !isFree && !_accountService.DeductCredits(accountId, cost))
        {
            return new RedoResponse
            {
                Status = "error",
                Message = "Failed to deduct credits",
                NewBalance = account.CreditBalance,
                WasFree = false
            };
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
            var (executionCode, undoCode, codeNeedsModeration, moderationReason) = 
                await _codeGenerator.GenerateCodeAsync(feedbackPrompt);

            // If the generated code contains filtered patterns, send to code moderation queue
            if (codeNeedsModeration)
            {
                _logger.LogInformation(
                    "[FIX-SECURITY] Generated code for fix by {Username} requires moderation. Reason: {Reason}",
                    account.Username, moderationReason);
                
                // Create placeholder command with PendingModeration status
                // The placeholder uses empty execution/undo code because the actual generated code
                // is stored in the moderation queue and will be applied to this command upon approval.
                // This prevents unapproved code from being visible or executable while pending review.
                var placeholderCommand = _commandQueue.AddCommandWithStatus(
                    userPrompt: originalCommand.UserPrompt,
                    executionCode: "", // Will be set after approval
                    undoCode: "", // Will be set after approval
                    source: originalCommand.Source,
                    author: originalCommand.Author,
                    imageContext: originalCommand.ImageContext,
                    userId: accountId,
                    aiResponse: "⏳ Waiting for code moderation approval...",
                    status: CommandStatus.PendingModeration,
                    queueForExecution: false); // Don't queue yet
                
                // Add to code moderation queue with the actual generated code
                // Upon approval, the code will be transferred from the queue to the placeholder command
                _codeModerationService.AddPendingCode(
                    originalCommand.UserPrompt,
                    executionCode,
                    undoCode,
                    moderationReason ?? "Filtered content detected",
                    originalCommand.Source,
                    originalCommand.Author,
                    accountId,
                    placeholderCommand.Id);
                
                // Mark as redo
                placeholderCommand.IsRedo = true;
                placeholderCommand.OriginalCommandId = commandId;
                placeholderCommand.RedoFeedback = feedback;
                
                var accountAfterModeration = _accountService.GetAccountById(accountId);
                
                _logger.LogInformation(
                    "[FIX] User {Username} requested fix for command #{OriginalId}. New command #{NewId} pending moderation. Free: {IsFree}", 
                    account.Username, commandId, placeholderCommand.Id, isFree);
                
                return new RedoResponse
                {
                    Status = "success",
                    Message = isFree 
                        ? "Free fix submitted! The generated code requires moderator approval." 
                        : $"Fix submitted! ${cost:F2} deducted. The generated code requires moderator approval.",
                    NewCommandId = placeholderCommand.Id,
                    NewBalance = accountAfterModeration?.CreditBalance ?? account.CreditBalance,
                    WasFree = isFree
                };
            }

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
                "[FIX] User {Username} requested fix for command #{OriginalId}. New command #{NewId}. Free: {IsFree}", 
                account.Username, commandId, newCommand.Id, isFree);

            return new RedoResponse
            {
                Status = "success",
                Message = isFree 
                    ? "Free fix submitted! Your next fix will cost credits." 
                    : $"Fix submitted! ${cost:F2} deducted.",
                NewCommandId = newCommand.Id,
                NewBalance = updatedAccount?.CreditBalance ?? account.CreditBalance,
                WasFree = isFree
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[FIX] Failed to generate fix code for command #{CommandId}", commandId);
            
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
