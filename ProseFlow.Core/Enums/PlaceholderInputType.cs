namespace ProseFlow.Core.Enums;

/// <summary>
/// Defines the type of input required for an action placeholder at runtime.
/// </summary>
public enum PlaceholderInputType
{
    /// <summary>
    /// A single-line text input.
    /// </summary>
    Text,

    /// <summary>
    /// A multi-line text input area.
    /// </summary>
    MultilineText,

    /// <summary>
    /// A selection from a predefined list of options.
    /// </summary>
    Choice,

    /// <summary>
    /// A true/false toggle.
    /// </summary>
    Boolean,

    /// <summary>
    /// A numeric input (integer or decimal).
    /// </summary>
    Number,

    /// <summary>
    /// A date selection control.
    /// </summary>
    DatePicker,

    /// <summary>
    /// An input that opens a system file picker dialog.
    /// </summary>
    FilePicker
}