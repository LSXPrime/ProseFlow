namespace ProseFlow.Core.Models;

/// <summary>
/// A record representing a single smart token for use in placeholder default values.
/// </summary>
/// <param name="Name">The user-friendly name of the token (e.g., "Clipboard Content").</param>
/// <param name="Token">The actual token syntax to be inserted (e.g., "{clipboard}").</param>
/// <param name="Description">A brief explanation of what the token does.</param>
public record SmartTokenInfo(string Name, string Token, string Description);

/// <summary>
/// Provides a static, centralized list of all available smart tokens in the application.
/// </summary>
public static class SmartTokens
{
    /// <summary>
    /// A list of all supported smart tokens.
    /// </summary>
    public static readonly IReadOnlyList<SmartTokenInfo> All =
    [
        new(
            "Clipboard Content",
            "{clipboard}",
            "Inserts the text currently on the user's clipboard."),
        new(
            "Current Date",
            "{date:yyyy-MM-dd}",
            "Inserts the current date. You can customize the format (e.g., MMMM d, yyyy)."),
        new(
            "Active Application",
            "{appContext}",
            "Inserts the process name of the application that is currently active.")
    ];
}