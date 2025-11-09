using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ProseFlow.Core.Interfaces.Os;
using SharpHook;
using KeyCode = SharpHook.Data.KeyCode;

namespace ProseFlow.Infrastructure.Services.Os.Clipboard;

/// <summary>
/// Implements clipboard interactions with a multi-layered fallback mechanism for robustness.
/// It attempts operations in the following order: Avalonia API -> Native Shell -> TextCopy.
/// </summary>
public sealed class ClipboardService(
    ILogger<ClipboardService> logger,
    [FromKeyedServices("NativeShellClipboardService")] IFallbackClipboardService nativeShellClipboardService,
    [FromKeyedServices("AvaloniaClipboardService")] IFallbackClipboardService avaloniaClipboardService,
    [FromKeyedServices("TextCopyClipboardService")] IFallbackClipboardService textCopyClipboardService) : IClipboardService
{
    private readonly EventSimulator _simulator = new();

    /// <inheritdoc />
    public async Task<string?> GetSelectedTextAsync()
    {
        var originalClipboardText = await GetClipboardTextAsync();

        // Clear clipboard temporarily to reliably detect if copy worked
        await SetClipboardTextAsync(string.Empty);

        SimulateCopyKeyPressAsync();

        // Give the OS a moment to process the copy
        await Task.Delay(150);

        var selectedText = await GetClipboardTextAsync();

        // Restore original clipboard content if it existed
        if (originalClipboardText != null) await SetClipboardTextAsync(originalClipboardText);

        // If the clipboard has new, non-empty content, it's our selected text.
        return !string.IsNullOrEmpty(selectedText) ? selectedText : null;
    }

    /// <inheritdoc />
    public async Task PasteTextAsync(string text)
    {
        // Use platform-specific clipboard handling to avoid WSL issues.
        var originalClipboardText = await GetClipboardTextAsync();

        await SetClipboardTextAsync(text);
        
        // Simulate paste key press to trigger the clipboard to paste the text.
        SimulatePasteKeyPressAsync();

        // Give the OS a moment to process the paste, then restore the clipboard.
        await Task.Delay(150);
        if (originalClipboardText != null) await SetClipboardTextAsync(originalClipboardText);
    }

    #region Simulation Helpers

    
    /// <summary>
    /// Simulates a copy key press (Ctrl+C or Cmd+C) to trigger the clipboard to copy the selected text.
    /// </summary>
    private void SimulateCopyKeyPressAsync()
    {
        var modifier = OperatingSystem.IsMacOS() ? KeyCode.VcLeftMeta : KeyCode.VcLeftControl;
        _simulator.SimulateKeyPress(modifier);
        _simulator.SimulateKeyPress(KeyCode.VcC);
        _simulator.SimulateKeyRelease(KeyCode.VcC);
        _simulator.SimulateKeyRelease(modifier);
    }

    /// <summary>
    /// Simulates a paste key press (Ctrl+V or Cmd+V) to trigger the clipboard to paste the text.
    /// </summary>
    private void SimulatePasteKeyPressAsync()
    {
        var modifier = OperatingSystem.IsMacOS() ? KeyCode.VcLeftMeta : KeyCode.VcLeftControl;
        _simulator.SimulateKeyPress(modifier);
        _simulator.SimulateKeyPress(KeyCode.VcV);
        _simulator.SimulateKeyRelease(KeyCode.VcV);
        _simulator.SimulateKeyRelease(modifier);
    }

    #endregion

    #region Platform-Specific Fallback Clipboard Handling

    /// <inheritdoc />
    public async Task<string?> GetClipboardTextAsync()
    {
        return await TryGetAsync(avaloniaClipboardService.GetTextAsync,
            "Avalonia clipboard get failed or empty. Falling back.") 
               ?? await TryGetAsync(nativeShellClipboardService.GetTextAsync,
                   "Native shell clipboard get failed or empty. Falling back.")
               ?? await TryGetAsync(textCopyClipboardService.GetTextAsync,
                   "All clipboard 'get' methods failed or empty.", isFinalAttempt: true);
    }

    /// <summary>
    /// Sets text on the clipboard using a multi-tiered fallback strategy.
    /// It returns on the first successful attempt.
    /// </summary>
    private async Task SetClipboardTextAsync(string text)
    {
        if (await TrySetAsync(() => avaloniaClipboardService.SetTextAsync(text),
                "Avalonia clipboard set failed. Falling back.")) return;
        
        if (await TrySetAsync(() => nativeShellClipboardService.SetTextAsync(text),
                "Native shell clipboard set failed. Falling back.")) return;

        await TrySetAsync(() => textCopyClipboardService.SetTextAsync(text),
            "All clipboard 'set' methods failed.", isFinalAttempt: true);
    }

    #endregion

    #region Fallback Execution Helpers

    /// <summary>
    /// Wraps a clipboard 'get' operation, handling exceptions and logging.
    /// Returns null if the operation fails or yields empty/whitespace text.
    /// </summary>
    private async Task<string?> TryGetAsync(Func<Task<string?>> getAction, string failureMessage, bool isFinalAttempt = false)
    {
        try
        {
            var result = await getAction();
            
            // A valid result must have content. If not, treat as failure to allow fallback.
            return string.IsNullOrWhiteSpace(result) ? 
                throw new InvalidOperationException("Clipboard content is empty or whitespace.") 
                : result;
        }
        catch (Exception ex)
        {
            if (isFinalAttempt)
                logger.LogError(ex, failureMessage);
            else
                logger.LogWarning(ex, failureMessage);

            return null;
        }
    }

    /// <summary>
    /// Wraps a clipboard 'set' operation, handling exceptions and logging.
    /// Returns true on success and false on failure.
    /// </summary>
    private async Task<bool> TrySetAsync(Func<Task> setAction, string failureMessage, bool isFinalAttempt = false)
    {
        try
        {
            await setAction();
            return true;
        }
        catch (Exception ex)
        {
            if (isFinalAttempt)
                logger.LogError(ex, failureMessage);
            else
                logger.LogWarning(ex, failureMessage);
            
            return false;
        }
    }
    
    #endregion
}