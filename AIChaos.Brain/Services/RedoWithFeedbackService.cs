using AIChaos.Brain.Models;

namespace AIChaos.Brain.Services;

/// <summary>
/// Service for handling command redos with feedback.
/// First redo per user is free, subsequent redos cost credits.
/// Maintains conversational context across multiple fix attempts.
/// </summary>
public class RedoService
{
    private readonly AccountService _accountService;
    private readonly CommandQueueService _commandQueue;
    private readonly AiCodeGeneratorService _codeGenerator;
    private readonly PromptModerationService _promptModerationService;
    private readonly CodeModerationService _codeModerationService;
    private readonly OpenRouterService _openRouterService;
    private readonly SettingsService _settingsService;
    private readonly ILogger<RedoService> _logger;

    public RedoService(
        AccountService accountService,
        CommandQueueService commandQueue,
        AiCodeGeneratorService codeGenerator,
        PromptModerationService promptModerationService,
        CodeModerationService codeModerationService,
        OpenRouterService openRouterService,
        SettingsService settingsService,
        ILogger<RedoService> logger)
    {
        _accountService = accountService;
        _commandQueue = commandQueue;
        _codeGenerator = codeGenerator;
        _promptModerationService = promptModerationService;
        _codeModerationService = codeModerationService;
        _openRouterService = openRouterService;
        _settingsService = settingsService;
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

        // Build conversation history for this fix
        // Start with the system prompt that defines the AI's role
        var systemPrompt = AiCodeGeneratorService.GroundRules;
        
        // Get or initialize conversation history from the original command
        // This allows multiple fixes to build upon previous conversations
        var conversationHistory = new List<ChatMessage>();
        
        // If this is a subsequent fix on an already-fixed command, inherit the conversation
        if (originalCommand.FixConversationHistory.Any())
        {
            _logger.LogInformation(
                "[FIX-CONVERSATION] Continuing conversation thread with {Count} previous messages for command #{CommandId}",
                originalCommand.FixConversationHistory.Count, commandId);
            conversationHistory.AddRange(originalCommand.FixConversationHistory);
        }
        else
        {
            // First fix - initialize conversation with the original request
            _logger.LogInformation(
                "[FIX-CONVERSATION] Starting new conversation thread for command #{CommandId}",
                commandId);
            conversationHistory.Add(new ChatMessage
            {
                Role = "system",
                Content = systemPrompt
            });
            conversationHistory.Add(new ChatMessage
            {
                Role = "user",
                Content = $"Original request: {originalCommand.UserPrompt}\n\nPlease generate Lua code for Garry's Mod that fulfills this request. Format your response as:\n```lua\n[execution code]\n---UNDO---\n[undo code]\n```"
            });
            
            // Add the assistant's previous response (the code that didn't work)
            if (!string.IsNullOrEmpty(originalCommand.ExecutionCode))
            {
                var previousCode = originalCommand.ExecutionCode;
                if (!string.IsNullOrEmpty(originalCommand.UndoCode))
                {
                    previousCode += "\n---UNDO---\n" + originalCommand.UndoCode;
                }
                conversationHistory.Add(new ChatMessage
                {
                    Role = "assistant",
                    Content = $"```lua\n{previousCode}\n```"
                });
            }
        }
        
        // Add the new user feedback to continue the conversation
        conversationHistory.Add(new ChatMessage
        {
            Role = "user",
            Content = $"The previous code didn't work as expected. User feedback:\n\n{feedback}\n\nPlease generate improved code that addresses this feedback. Format your response as:\n```lua\n[execution code]\n---UNDO---\n[undo code]\n```"
        });

        try
        {
            _logger.LogDebug("[FIX] Generating code with {MessageCount} conversation messages", conversationHistory.Count);
            
            // Use OpenRouterService directly to maintain conversation context
            var response = await _openRouterService.ChatCompletionAsync(conversationHistory);
            
            if (string.IsNullOrEmpty(response))
            {
                _logger.LogError("[FIX] OpenRouter returned empty response for command #{CommandId}", commandId);
                
                // Refund credits if we already deducted them
                if (!isSingleUserMode && !isFree)
                {
                    _accountService.AddCredits(accountId, cost);
                }
                
                return new RedoResponse
                {
                    Status = "error",
                    Message = "Failed to generate fix code. Credits have been refunded.",
                    NewBalance = account.CreditBalance + (isFree ? 0 : cost),
                    WasFree = isFree
                };
            }
            
            // Add assistant's response to conversation history for future fixes
            conversationHistory.Add(new ChatMessage
            {
                Role = "assistant",
                Content = response
            });
            
            // Parse the code from the response
            var code = OpenRouterService.CleanLuaCode(response);
            string executionCode;
            string undoCode;
            
            // Parse execution and undo code
            if (code.Contains("---UNDO---"))
            {
                var parts = code.Split("---UNDO---");
                executionCode = parts[0].Trim();
                undoCode = parts.Length > 1 ? parts[1].Trim() : "print(\"No undo code provided\")";
            }
            else
            {
                executionCode = code;
                undoCode = "print(\"No undo code provided\")";
            }
            
            // Check for dangerous patterns first (always block these)
            var dangerousReason = CodeModerationService.GetDangerousPatternReason(executionCode);
            if (dangerousReason != null)
            {
                _logger.LogWarning("[FIX] AI generated code containing dangerous patterns: {Reason}. Blocking.", dangerousReason);
                
                // Refund credits
                if (!isSingleUserMode && !isFree)
                {
                    _accountService.AddCredits(accountId, cost);
                }
                
                return new RedoResponse
                {
                    Status = "error",
                    Message = $"Generated code would break the game: {dangerousReason}. Credits have been refunded.",
                    NewBalance = account.CreditBalance + (isFree ? 0 : cost),
                    WasFree = isFree
                };
            }
            
            // Check for filtered patterns (send to moderation if BlockLinksInGeneratedCode is enabled)
            bool codeNeedsModeration = false;
            string? moderationReason = null;
            var settings = _settingsService.Settings;
            if (settings.General.BlockLinksInGeneratedCode)
            {
                moderationReason = CodeModerationService.GetFilteredPatternReason(executionCode);
                if (moderationReason != null)
                {
                    _logger.LogInformation("[FIX] AI generated code with filtered content: {Reason}. Sending to moderation.", moderationReason);
                    codeNeedsModeration = true;
                }
            }

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
                
                // Mark as redo and store conversation history
                placeholderCommand.IsRedo = true;
                placeholderCommand.OriginalCommandId = commandId;
                placeholderCommand.RedoFeedback = feedback;
                placeholderCommand.FixConversationHistory = conversationHistory;
                
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

            // Mark as redo and store conversation history
            newCommand.IsRedo = true;
            newCommand.OriginalCommandId = commandId;
            newCommand.RedoFeedback = feedback;
            newCommand.FixConversationHistory = conversationHistory;

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
