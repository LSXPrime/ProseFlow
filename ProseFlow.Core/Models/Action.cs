using ProseFlow.Core.Abstracts;
using ProseFlow.Core.Enums;

namespace ProseFlow.Core.Models;

/// <summary>
/// Represents a user-defined AI task or "Action".
/// </summary>
public class Action : EntityBase
{
    /// <summary>
    /// The unique, user-facing name of the action (e.g., "Proofread", "Summarize").
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// A short phrase prepended to the user's selected text before sending to the AI.
    /// For generation actions, this can serve as the primary prompt or be combined with user input from the menu.
    /// </summary>
    public string Prefix { get; set; } = string.Empty;

    /// <summary>
    /// The core set of rules and guidelines (system prompt) for the AI.
    /// May contain placeholders like [placeholderName] to be filled at runtime.
    /// </summary>
    public string Instruction { get; set; } = string.Empty;

    /// <summary>
    /// The resource path or identifier for the action's icon.
    /// </summary>
    public string Icon { get; set; } = "avares://ProseFlow/Assets/Icons/default.svg";
    
    /// <summary>
    /// The desired output mode for the result (e.g., InPlace, Windowed, Diff).
    /// The default is <see cref="OutputMode.InPlace"/>.
    /// </summary>
    /// <remarks>
    /// This property specifies how the AI's response will be displayed to the user.
    /// - <see cref="OutputMode.InPlace"/>: The AI's response will replace the selected text.
    /// - <see cref="OutputMode.Windowed"/>: The AI's response will be displayed in a new window.
    /// - <see cref="OutputMode.Diff"/>: The AI's response will be displayed in a diff view window.
    /// </remarks>
    public OutputMode OutputMode { get; set; } = OutputMode.InPlace;

    /// <summary>
    /// If true, the prompt will be augmented to ask the AI to explain its changes.
    /// </summary>
    public bool ExplainChanges { get; set; }
    
    /// <summary>
    /// If true, this action will be prioritized in UIs like the Arc Menu.
    /// </summary>
    public bool IsFavorite { get; set; }

    /// <summary>
    /// If true, this action requires text to be selected before it can be executed.
    /// If false, it can be used to generate text from scratch.
    /// </summary>
    public bool RequiresSelection { get; set; } = false;

    /// <summary>
    /// A list of application process names where this action should be prioritized or exclusively shown.
    /// An empty list means the action is globally available.
    /// </summary>
    public List<string> ApplicationContext { get; set; } = [];

    /// <summary>
    /// The display order of the action within its group.
    /// </summary>
    public int SortOrder { get; set; }
    
    /// <summary>
    /// The foreign key for the group this action belongs to.
    /// </summary>
    public int ActionGroupId { get; set; } = 1;
    
    /// <summary>
    /// The navigation property for the group.
    /// </summary>
    public ActionGroup? ActionGroup { get; set; }

    /// <summary>
    /// A collection of dynamic placeholders defined for this action's instruction.
    /// </summary>
    public ICollection<ActionPlaceholder> Placeholders { get; set; } = new List<ActionPlaceholder>();
}