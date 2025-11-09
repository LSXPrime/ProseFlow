namespace ProseFlow.Core.Models;

public static class Constants
{
    public const string AppName = "ProseFlow";
    public const string AppVersion = "0.3.0";
    public const string AppDescription = "Your personal writing assistant, available everywhere with a keystroke.";
    public const string AppAuthor = "LSXPrime";
    public const string AppWebsite = "https://lsxprime.github.io/proseflow-web/";
    public const string AppCopyright = "Copyright © 2025 LSXPrime";
    public const string AppLicense = "AGPL v3.0";
    public const string AppRepository = "https://github.com/LSXPrime/ProseFlow/";
    public const string ManifestUrl = "https://raw.githubusercontent.com/LSXPrime/ProseFlow/refs/heads/master/model-manifest.json";
    public const string AppIssuesUrl = "https://github.com/LSXPrime/ProseFlow/issues";
    public const string AppSponsorsUrl = "https://github.com/LSXPrime/ProseFlow/?tab=readme-ov-file#%EF%B8%8F-support-this-project";
    public const string AppDonationUrl = "https://ko-fi.com/lsxprime";
    public const string AppLicenseUrl = "https://github.com/LSXPrime/ProseFlow/blob/master/LICENSE.md";
    
    /// <summary>
    /// The name used to identify ad-hoc "Custom Instruction" actions in the history.
    /// </summary>
    public const string CustomInstructionActionName = "Custom Instruction";
    
    /// <summary>
    /// A collection of file extensions for document types that can be read directly as plain text.
    /// </summary>
    public static readonly string[] PlainTextLikeExtensions =
    {
        ".txt", ".md", ".json", ".xml", ".html", ".css", ".js", ".cs", ".py", ".java", ".rtf",
        ".csv", ".tsv", ".ini", ".yml", ".yaml", ".log", ".sql", ".rb", ".php", ".go", ".ts", ".svg"
    };

    /// <summary>
    /// A comprehensive collection of all document file extensions the application can read and extract text from.
    /// Useful for file dialog filters.
    /// </summary>
    public static readonly string[] SupportedDocumentExtensions = [..PlainTextLikeExtensions, ".pdf", ".docx", ".xlsx", ".xls", ".epub"];
    
    public static string LogDirectoryPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        AppName,
        "logs");
}