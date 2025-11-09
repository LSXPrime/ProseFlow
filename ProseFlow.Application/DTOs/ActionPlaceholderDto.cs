using System.Text.Json.Serialization;
using ProseFlow.Core.Enums;

namespace ProseFlow.Application.DTOs;

/// <summary>
/// Data Transfer Object for an ActionPlaceholder, used in import/export.
/// </summary>
public record ActionPlaceholderDto
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("label")]
    public string Label { get; init; } = string.Empty;

    [JsonPropertyName("type")]
    public PlaceholderInputType InputType { get; init; }

    [JsonPropertyName("options")]
    public IEnumerable<string> Options { get; init; } = [];

    [JsonPropertyName("default")]
    public string? DefaultValue { get; init; }

    [JsonPropertyName("validation_json")]
    public string? ValidationJson { get; init; }

    [JsonPropertyName("display_condition_json")]
    public string? DisplayConditionJson { get; init; }
}