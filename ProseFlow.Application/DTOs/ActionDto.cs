using System.Text.Json.Serialization;
using ProseFlow.Core.Enums;

namespace ProseFlow.Application.DTOs;

/// <summary>
/// Data Transfer Object representing an Action for import/export operations.
/// This matches the structure of the JSON configuration file.
/// </summary>
public record ActionDto
{
    [JsonPropertyName("prefix")]
    public string Prefix { get; init; } = string.Empty;

    [JsonPropertyName("instruction")]
    public string Instruction { get; init; } = string.Empty;

    [JsonPropertyName("icon")]
    public string Icon { get; init; } = "avares://ProseFlow/Assets/Icons/default.svg";

    [JsonPropertyName("output_mode")]
    public OutputMode OutputMode { get; init; }

    [JsonPropertyName("explain_changes")]
    public bool ExplainChanges { get; init; }

    [JsonPropertyName("requires_selection")]
    public bool RequiresSelection { get; init; } = true;

    [JsonPropertyName("application_context")]
    public IEnumerable<string> ApplicationContext { get; init; } = [];

    [JsonPropertyName("placeholders")]
    public IEnumerable<ActionPlaceholderDto> Placeholders { get; init; } = [];
}