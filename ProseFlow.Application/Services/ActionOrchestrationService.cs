using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ProseFlow.Application.DTOs;
using ProseFlow.Application.Events;
using ProseFlow.Application.Interfaces;
using ProseFlow.Core.Enums;
using ProseFlow.Core.Interfaces;
using ProseFlow.Core.Interfaces.Os;
using ProseFlow.Core.Models;
using Action = ProseFlow.Core.Models.Action;

namespace ProseFlow.Application.Services;

public class ActionOrchestrationService : IDisposable
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IReadOnlyDictionary<string, IAiProvider> _providers;
    private readonly ILocalSessionService _localSessionService;
    private readonly IBackgroundActionTrackerService _trackerService;
    private readonly ILogger<ActionOrchestrationService> _logger;
    
    private readonly IHotkeyService _hotkeyService;
    private readonly IActiveWindowService _activeWindowService;
    private readonly IClipboardService _clipboardService;

    public ActionOrchestrationService(IServiceScopeFactory scopeFactory, IHotkeyService hotkeyService, IActiveWindowService activeWindowService,
        IClipboardService clipboardService, IEnumerable<IAiProvider> providers, ILocalSessionService localSessionService,
        IBackgroundActionTrackerService trackerService, ILogger<ActionOrchestrationService> logger)
    {
        _scopeFactory = scopeFactory;
        _hotkeyService = hotkeyService;
        _activeWindowService = activeWindowService;
        _clipboardService = clipboardService;
        _localSessionService = localSessionService;
        _trackerService = trackerService;
        _providers = providers.ToDictionary(p => p.Name, p => p, StringComparer.OrdinalIgnoreCase);
        _logger = logger;
    }

    public void Initialize()
    {
        _hotkeyService.ActionMenuHotkeyPressed += async () => await HandleActionMenuHotkeyAsync();
        _hotkeyService.SmartPasteHotkeyPressed += async () => await HandleSmartPasteHotkeyAsync();
    }

    public async Task HandleActionMenuHotkeyAsync()
    {
        // Notify listeners that the menu is about to open.
        AppEvents.OnFloatingMenuStateChanged(true);
        try
        {
            await Task.Delay(200);
            var selectedText = await _clipboardService.GetSelectedTextAsync();
            var isGenerationMode = string.IsNullOrWhiteSpace(selectedText);

            var activeAppContext = await _activeWindowService.GetActiveWindowProcessNameAsync();
            var allActions = await ExecuteQueryAsync(unitOfWork => unitOfWork.Actions.GetAllOrderedAsync());

            // Filter actions based on context and mode (generation vs. transformation).
            var availableActions = allActions
                .Where(a => !isGenerationMode || !a.RequiresSelection)
                .Where(a => a.ApplicationContext.Count == 0 ||
                            a.ApplicationContext.Contains(activeAppContext, StringComparer.OrdinalIgnoreCase))
                .OrderByDescending(a => a.IsFavorite)
                .ThenBy(a => a.ActionGroup!.SortOrder)
                .ThenBy(a => a.SortOrder)
                .ToList();

            var request = await AppEvents.RequestFloatingMenuAsync(availableActions, activeAppContext, isGenerationMode);
            if (request is not null)
                await ProcessRequestAsync(request, isGenerationMode, selectedText);
        }
        finally
        {
            // Ensure listeners are notified that the menu has closed, regardless of outcome.
            AppEvents.OnFloatingMenuStateChanged(false);
        }
    }

    private async Task HandleSmartPasteHotkeyAsync()
    {
        var result = await ExecuteQueryAsync(async unitOfWork =>
        {
            var settings = await unitOfWork.Settings.GetGeneralSettingsAsync();
            if (settings.SmartPasteActionId is null)
                return new { Action = (Action?)null, IsConfigured = false };

            var action = (await unitOfWork.Actions
                .GetByExpressionAsync(a => a.Id == settings.SmartPasteActionId.Value)).FirstOrDefault();
            
            return new { Action = action, IsConfigured = true };
        });

        if (!result.IsConfigured)
        {
            AppEvents.RequestNotification("Smart Paste action not configured in settings.", NotificationType.Warning);
            return;
        }

        if (result.Action is null)
        {
            AppEvents.RequestNotification("The configured Smart Paste action was not found.", NotificationType.Error);
            return;
        }

        var request = new ActionExecutionRequest(result.Action, OutputMode.InPlace, null); // Smart Paste is always InPlace
        await ProcessRequestAsync(request, false);
    }

    /// <summary>
    /// Processes a request to execute an action.
    /// </summary>
    /// <param name="request">The action execution request.</param>
    /// <param name="isGenerationMode">If true, the action is being executed in generation mode (no selection).</param>
    /// <param name="inputTextOverride">If provided, this text is used as input instead of getting text from the clipboard. Ideal for drag-and-drop or pre-captured text.</param>
    public async Task ProcessRequestAsync(ActionExecutionRequest request, bool isGenerationMode, string? inputTextOverride = null)
    {
        var overallStopwatch = Stopwatch.StartNew();
        AppEvents.RequestNotification($"Processing '{request.ActionToExecute.Name}' ...", NotificationType.Info);
        var trackedAction = _trackerService.AddAction(request.ActionToExecute.Name, request.ActionToExecute.Icon);

        // Local stateful session ID (If local provider is used)
        Guid? localSessionId = null; 
        
        try
        {
            string? userInput;
            if (request.ActionToExecute.RequiresSelection || !isGenerationMode)
            {
                // For selection-based actions, get the input text.
                if (inputTextOverride is null) await Task.Delay(200); // Wait for focus
                userInput = inputTextOverride ?? await _clipboardService.GetSelectedTextAsync();
                if (string.IsNullOrWhiteSpace(userInput))
                {
                    AppEvents.RequestNotification("No text selected or clipboard is empty.", NotificationType.Warning);
                    _trackerService.CompleteAction(trackedAction.Id, ActionStatus.Error, TimeSpan.FromSeconds(2));
                    return;
                }
            }
            else
            {
                // For generation actions, the user input is conceptually empty.
                userInput = string.Empty;
            }
            
            _trackerService.UpdateStatus(trackedAction.Id, ActionStatus.Processing);

            // Initialize the conversation transcript
            var conversationHistory = new List<ChatMessage>();

            // Add the system prompt (the main rules).
            var systemInstruction = request.ActionToExecute.ExplainChanges
                ? $"{request.ActionToExecute.Instruction}\n\nIMPORTANT: After your main response, add a section that starts with '---EXPLANATION---' and explain the changes you made."
                : request.ActionToExecute.Instruction;
            conversationHistory.Add(new ChatMessage("system", systemInstruction));
            
            // For generation actions, userInput will be empty.
            var userMessage = $"{request.ActionToExecute.Prefix}{userInput}";
            conversationHistory.Add(new ChatMessage("user", userMessage));

            var outputMode = request.Mode == OutputMode.Default
                ? request.ActionToExecute.OutputMode
                : request.Mode;
            
            var wasSuccessful = false;

            switch (outputMode)
            {
                case OutputMode.Windowed:
                    var windowedResult = await HandleWindowedModeAsync(request, userInput, conversationHistory, trackedAction.Cts.Token, localSessionId);
                    wasSuccessful = windowedResult.Success;
                    localSessionId = windowedResult.SessionId;
                    break;
                case OutputMode.InPlace:
                    wasSuccessful = await HandleInPlaceModeAsync(request, userInput, conversationHistory, trackedAction.Cts.Token);
                    break;
                case OutputMode.Diff:
                    var diffResult = await HandleDiffModeAsync(request, userInput, conversationHistory, trackedAction.Cts.Token, localSessionId);
                    wasSuccessful = diffResult.Success;
                    localSessionId = diffResult.SessionId;
                    break;
            }

            overallStopwatch.Stop();
            _logger.LogInformation("Action '{ActionName}' completed in {ElapsedSeconds:F2}s.", request.ActionToExecute.Name, overallStopwatch.Elapsed.TotalSeconds);
            var finalStatus = wasSuccessful ? ActionStatus.Success : ActionStatus.Error;
            _trackerService.CompleteAction(trackedAction.Id, finalStatus, TimeSpan.FromSeconds(2));
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Action '{ActionName}' was cancelled by the user.", request.ActionToExecute.Name);
            _trackerService.CompleteAction(trackedAction.Id, ActionStatus.Error, TimeSpan.FromSeconds(0.1));
        }
        catch (Exception ex)
        {
            overallStopwatch.Stop();
            _logger.LogError(ex, "Error executing action: {ActionName}", request.ActionToExecute.Name);
            var displayMessage = ex is InvalidOperationException ? ex.Message : "An unexpected error occurred.";
            AppEvents.RequestNotification($"Error: {displayMessage}", NotificationType.Error);
            _trackerService.CompleteAction(trackedAction.Id, ActionStatus.Error, TimeSpan.FromSeconds(2));
        }
        finally
        {
            // Clean up the stateful session if one was created.
            if (localSessionId.HasValue) _localSessionService.EndSession(localSessionId.Value);
        }
    }

    private async Task<(bool Success, Guid? SessionId)> HandleWindowedModeAsync(ActionExecutionRequest request, string userInput, List<ChatMessage> conversationHistory, CancellationToken cancellationToken, Guid? localSessionId)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var executionResult = await ExecuteRequestWithFallbackAsync(conversationHistory, request.ProviderOverride, cancellationToken, localSessionId);
            
            // Check for cancellation requests that occurred during generation but after the stream completed.
            cancellationToken.ThrowIfCancellationRequested();

            if (executionResult is null)
            {
                AppEvents.RequestNotification("All available AI providers failed.", NotificationType.Error);
                return (false, localSessionId);
            }

            if (executionResult.Value.Provider.Type == ProviderType.Local && localSessionId is null)
            {
                localSessionId = _localSessionService.StartSession();
                if (localSessionId is null)
                {
                    AppEvents.RequestNotification("Failed to start a local model session.", NotificationType.Error);
                    return (false, null);
                }
            }

            var (aiResponse, provider, latencyMs) = executionResult.Value;
            conversationHistory.Add(new ChatMessage("assistant", aiResponse.Content));

            await LogToHistoryAsync(
                request.ActionToExecute.Name,
                request.ActionToExecute.Instruction,
                provider.Name,
                aiResponse.ProviderName,
                userInput,
                aiResponse.Content,
                aiResponse.PromptTokens,
                aiResponse.CompletionTokens,
                latencyMs,
                aiResponse.TokensPerSecond);

            var (mainOutput, explanation) = ParseOutput(aiResponse.Content, request.ActionToExecute.ExplainChanges);
            var windowData = new ResultWindowData(request.ActionToExecute.Name, mainOutput, explanation);
            var refinementRequest = await AppEvents.RequestResultWindowAsync(windowData);

            if (refinementRequest is null) break; // User cancelled, which is a successful outcome.

            conversationHistory.Add(new ChatMessage("user", refinementRequest.NewInstruction));
        }
        return (true, localSessionId);
    }

    private async Task<bool> HandleInPlaceModeAsync(ActionExecutionRequest request, string userInput, List<ChatMessage> conversationHistory, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var executionResult = await ExecuteRequestWithFallbackAsync(conversationHistory, request.ProviderOverride, cancellationToken);

        // Check for cancellation requests that occurred during generation but after the stream completed.
        cancellationToken.ThrowIfCancellationRequested();

        if (executionResult is null)
        {
            AppEvents.RequestNotification("All available AI providers failed.", NotificationType.Error);
            return false;
        }

        var (aiResponse, provider, latencyMs) = executionResult.Value;

        await LogToHistoryAsync(
            request.ActionToExecute.Name,
            request.ActionToExecute.Instruction,
            provider.Name,
            aiResponse.ProviderName,
            userInput,
            aiResponse.Content,
            aiResponse.PromptTokens,
            aiResponse.CompletionTokens,
            latencyMs,
            aiResponse.TokensPerSecond);
        
        await _clipboardService.PasteTextAsync(aiResponse.Content);
        return true;
    }
    
    private async Task<(bool Success, Guid? SessionId)> HandleDiffModeAsync(ActionExecutionRequest request, string originalInput, List<ChatMessage> conversationHistory, CancellationToken cancellationToken, Guid? localSessionId)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var executionResult = await ExecuteRequestWithFallbackAsync(conversationHistory, request.ProviderOverride, cancellationToken, localSessionId);

            // Check for cancellation requests that occurred during generation but after the stream completed.
            cancellationToken.ThrowIfCancellationRequested();

            if (executionResult is null)
            {
                AppEvents.RequestNotification("All available AI providers failed.", NotificationType.Error);
                return (false, localSessionId);
            }
            
            var (aiResponse, provider, latencyMs) = executionResult.Value;
            
            if (provider.Type == ProviderType.Local && localSessionId is null)
            {
                localSessionId = _localSessionService.StartSession();
                if (localSessionId is null)
                {
                    AppEvents.RequestNotification("Failed to start a local model session.", NotificationType.Error);
                    return (false, null);
                }
            }
            
            var diffData = new DiffViewData(request.ActionToExecute.Name, originalInput, aiResponse.Content);
            var userDecision = await AppEvents.RequestDiffViewAsync(diffData);

            switch (userDecision)
            {
                case Accepted accepted:
                    await LogToHistoryAsync(
                        request.ActionToExecute.Name,
                        request.ActionToExecute.Instruction,
                        provider.Name,
                        aiResponse.ProviderName,
                        originalInput,
                        accepted.NewText,
                        aiResponse.PromptTokens,
                        aiResponse.CompletionTokens,
                        latencyMs,
                        aiResponse.TokensPerSecond);
                    await _clipboardService.PasteTextAsync(accepted.NewText);
                    return (true, localSessionId); 
                
                case Refined refined:
                    // The last message in history is the assistant's previous response. Add it before the user's refinement.
                    conversationHistory.Add(new ChatMessage("assistant", aiResponse.Content));
                    conversationHistory.Add(new ChatMessage("user", refined.RefinementInstruction));
                    continue;

                case Regenerated:
                    continue;

                case Cancelled or null:
                    return (true, localSessionId); // User cancellation is a successful outcome.
            }
        }
    }


    /// <summary>
    /// Executes an AI request, trying the primary provider first and then the fallback provider upon failure.
    /// </summary>
    /// <returns>A tuple containing the response, the successful provider, and latency, or null if all attempts fail.</returns>
    private async Task<(AiResponse Response, IAiProvider Provider, double LatencyMs)?> ExecuteRequestWithFallbackAsync(
        List<ChatMessage> messages, string? providerOverride, CancellationToken cancellationToken, Guid? sessionId = null)
    {
        var settings = await ExecuteQueryAsync(unitOfWork => unitOfWork.Settings.GetProviderSettingsAsync());

        // 1. Determine Primary Provider
        var primaryProvider = await GetProviderAsync(providerOverride, settings.PrimaryServiceType);

        if (primaryProvider is not null)
            try
            {
                var stopwatch = Stopwatch.StartNew();
                var response = await primaryProvider.GenerateResponseAsync(messages, cancellationToken, sessionId);
                stopwatch.Stop();
                _logger.LogInformation("Primary provider '{ProviderName}' succeeded.", primaryProvider.Name);
                return (response, primaryProvider, stopwatch.Elapsed.TotalMilliseconds);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Primary provider '{ProviderName}' failed. Attempting fallback.", primaryProvider.Name);
                AppEvents.RequestNotification($"Primary provider ({primaryProvider.Name}) failed. Trying fallback...", NotificationType.Warning);
            }

        // 2. Determine and Try Fallback Provider
        if (settings.FallbackServiceType.Equals("None", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("No fallback provider is configured.");
            return null;
        }
        
        var fallbackProvider = await GetProviderAsync(null, settings.FallbackServiceType); // No override for fallback

        if (fallbackProvider is not null && fallbackProvider.Name != primaryProvider?.Name)
            try
            {
                var stopwatch = Stopwatch.StartNew();
                var response = await fallbackProvider.GenerateResponseAsync(messages, cancellationToken, sessionId);
                stopwatch.Stop();
                _logger.LogInformation("Fallback provider '{ProviderName}' succeeded.", fallbackProvider.Name);
                return (response, fallbackProvider, stopwatch.Elapsed.TotalMilliseconds);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Fallback provider '{ProviderName}' also failed.", fallbackProvider.Name);
            }

        // 3. Both failed or were not configured
        return null;
    }
    
    private (string MainOutput, string? Explanation) ParseOutput(string rawOutput, bool expectExplanation)
    {
        if (!expectExplanation || !rawOutput.Contains("---EXPLANATION---")) return (rawOutput.Trim(), null);
        
        var parts = rawOutput.Split(["---EXPLANATION---"], 2, StringSplitOptions.None);
        return (parts[0].Trim(), parts[1].Trim());
    }

    private Task<IAiProvider?> GetProviderAsync(string? providerOverride, string serviceType)
    {
        if (!string.IsNullOrWhiteSpace(providerOverride) &&
            _providers.TryGetValue(providerOverride, out var overriddenProvider))
            return Task.FromResult<IAiProvider?>(overriddenProvider);

        if (_providers.TryGetValue(serviceType, out var provider))
            return Task.FromResult<IAiProvider?>(provider);

        return Task.FromResult<IAiProvider?>(null);
    }
    
    private async Task LogToHistoryAsync(string actionName, string instruction, string providerType, string modelUsed, string input, string output, long promptTokens, long completionTokens, double latencyMs, double inferenceSpeed)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var historyService = scope.ServiceProvider.GetRequiredService<HistoryService>();
            await historyService.AddHistoryEntryAsync(actionName, instruction, providerType, modelUsed, input, output, promptTokens, completionTokens, latencyMs, inferenceSpeed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log history entry.");
            AppEvents.RequestNotification("Failed to log to history", NotificationType.Warning);
        }
    }

    public void Dispose()
    {
        _hotkeyService.Dispose();
        foreach (var provider in _providers.Values)
        {
            provider.Dispose();
        }
        GC.SuppressFinalize(this);
    }
    
    #region Private Helpers

    /// <summary>
    /// Creates a UoW scope and executes a query.
    /// </summary>
    private async Task<T> ExecuteQueryAsync<T>(Func<IUnitOfWork, Task<T>> query)
    {
        using var scope = _scopeFactory.CreateScope();
        await using var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        return await query(unitOfWork);
    }

    #endregion
}