namespace ProseFlow.Core.Interfaces.Os;

/// <summary>
/// Defines the contract for a service that handles clipboard interactions.
/// </summary>
public interface IClipboardService
{
    /// <summary>
    /// Gets text from the clipboard using a multi-tiered fallback strategy.
    /// An empty or whitespace result is considered a failure, triggering the next fallback.
    /// </summary>
    Task<string?> GetClipboardTextAsync();
    
    /// <summary>
    /// Attempts to get the currently selected text by simulating a copy action.
    /// </summary>
    /// <returns>The selected text, or null if no text is selected or could be copied.</returns>
    Task<string?> GetSelectedTextAsync();

    /// <summary>
    /// Pastes the specified text at the current cursor position by simulating a paste action.
    /// </summary>
    /// <param name="text">The text to paste.</param>
    Task PasteTextAsync(string text);
}