using ProseFlow.Core.Abstracts;
using ProseFlow.Core.Enums;

namespace ProseFlow.Core.Models;

/// <summary>
/// Represents a dynamic placeholder within an Action's instruction prompt.
/// </summary>
public class ActionPlaceholder : EntityBase
{
    /// <summary>
    /// The internal identifier for the placeholder (e.g., "language").
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// The user-facing question or label for this placeholder at runtime.
    /// </summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// The type of input required from the user at runtime.
    /// </summary>
    public PlaceholderInputType InputType { get; set; } = PlaceholderInputType.Text;

    /// <summary>
    /// A JSON-serialized list of strings representing the available options
    /// when the InputType is 'Choice'.
    /// </summary>
    public string OptionsJson { get; set; } = "[]";

    /// <summary>
    /// An optional pre-selected choice or pre-filled text to speed up common uses.
    /// Can contain special tokens like {clipboard} or {date:yyyy-MM-dd}.
    /// </summary>
    public string? DefaultValue { get; set; }
    
    /// <summary>
    /// A JSON string defining validation rules for the placeholder's input.
    /// Example: `{"required": true, "minLength": 10}`
    /// </summary>
    public string? ValidationJson { get; set; }
    
    /// <summary>
    /// A JSON string defining the condition under which this placeholder should be displayed.
    /// Example: `{"placeholderName": "email_type", "operator": "equals", "value": "Meeting Request"}`
    /// </summary>
    public string? DisplayConditionJson { get; set; }

    /// <summary>
    /// The foreign key for the Action this placeholder belongs to.
    /// </summary>
    public int ActionId { get; set; }

    /// <summary>
    /// The navigation property for the parent Action.
    /// </summary>
    public Action? Action { get; set; }
}