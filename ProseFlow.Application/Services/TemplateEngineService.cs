using System.Text.Json;
using ProseFlow.Core.Enums;
using ProseFlow.Core.Interfaces.Os;
using ProseFlow.Core.Models;
using Action = ProseFlow.Core.Models.Action;

namespace ProseFlow.Application.Services;

// Internal models for deserializing JSON rule properties
internal record ValidationRules(bool? Required, int? MinLength, int? MaxLength, double? MinValue, double? MaxValue);
internal record DisplayCondition(string PlaceholderName, string Operator, object Value);

/// <summary>
/// Manages the state and logic for executing a multistep Templated Action.
/// </summary>
public class TemplateEngineService(
    IClipboardService clipboardService, 
    IActiveWindowService activeWindowService)
{
    private Action? _templateAction;
    private readonly Dictionary<string, object?> _collectedValues = [];
    private readonly List<int> _stepHistory = [];
    private int _currentStepIndex = -1;

    /// <summary>
    /// Starts a new template session and returns the first step.
    /// </summary>
    /// <param name="templateAction">The action containing placeholders.</param>
    /// <returns>The index and placeholder for the first step, or null if no steps are valid.</returns>
    public (int Index, ActionPlaceholder Placeholder)? StartSession(Action templateAction)
    {
        _templateAction = templateAction;
        _collectedValues.Clear();
        _stepHistory.Clear();
        _currentStepIndex = -1;

        return GetNextStep();
    }

    /// <summary>
    /// Validates and stores the value for the current step, then determines and returns the next step.
    /// </summary>
    /// <param name="value">The value submitted by the user for the current step.</param>
    /// <returns>The index and placeholder for the next step, or null if the template is complete.</returns>
    public (int Index, ActionPlaceholder Placeholder)? SubmitStep(object? value)
    {
        if (_templateAction is null || _currentStepIndex == -1) return null;
        
        var currentPlaceholder = _templateAction.Placeholders.ElementAt(_currentStepIndex);
        _collectedValues[currentPlaceholder.Name] = value;
        _stepHistory.Add(_currentStepIndex);

        return GetNextStep();
    }
    
    /// <summary>
    /// Moves to the previous step in the history.
    /// </summary>
    /// <returns>The index and placeholder for the previous step, or null if at the beginning.</returns>
    public (int Index, ActionPlaceholder Placeholder)? GetPreviousStep()
    {
        if (_templateAction is null || _stepHistory.Count == 0)
        {
            _currentStepIndex = -1; // Reset state completely.
            return null;
        }

        // Get the index of the step we want to return to. This is the last item in our history.
        var indexToRevisit = _stepHistory.Last();
        _stepHistory.RemoveAt(_stepHistory.Count - 1);

        // Update the current step index to the one we are revisiting.
        _currentStepIndex = indexToRevisit;

        // Get the placeholder for this step.
        var placeholderToRevisit = _templateAction.Placeholders.ElementAt(indexToRevisit);

        // Remove the previously collected value for this step so the user can re-enter it.
        _collectedValues.Remove(placeholderToRevisit.Name);

        return (_currentStepIndex, placeholderToRevisit);
    }

    /// <summary>
    /// Asynchronously resolves any "smart default" tokens for a given placeholder.
    /// </summary>
    /// <param name="placeholder">The placeholder whose default value needs to be resolved.</param>
    /// <returns>The resolved default value.</returns>
    public async Task<string?> ResolveSmartDefaultAsync(ActionPlaceholder placeholder)
    {
        if (string.IsNullOrEmpty(placeholder.DefaultValue)) return null;

        return placeholder.DefaultValue switch
        {
            "{clipboard}" => await clipboardService.GetClipboardTextAsync(),
            var s when s.StartsWith("{date:") => DateTime.Now.ToString(s.Replace("{date:", "").Replace("}", "")),
            "{appContext}" => await activeWindowService.GetActiveWindowProcessNameAsync(),
            _ => placeholder.DefaultValue
        };
    }
    
    /// <summary>
    /// Validates a given value against the rules of a placeholder.
    /// </summary>
    /// <param name="placeholder">The placeholder containing the validation rules.</param>
    /// <param name="value">The value to validate.</param>
    /// <returns>An error message if validation fails; otherwise, null.</returns>
    public static string? Validate(ActionPlaceholder placeholder, object? value)
    {
        if (string.IsNullOrEmpty(placeholder.ValidationJson)) return null;
        
        var rules = JsonSerializer.Deserialize<ValidationRules>(placeholder.ValidationJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (rules is null) return null;

        var valueStr = value?.ToString() ?? string.Empty;

        // General 'Required' check for textual and file inputs
        if (placeholder.InputType is not PlaceholderInputType.Number && rules.Required == true && string.IsNullOrWhiteSpace(valueStr))
        {
            return placeholder.InputType == PlaceholderInputType.FilePicker
                ? "A file must be selected."
                : "This field is required.";
        }

        // Text-based length validation
        if (placeholder.InputType is PlaceholderInputType.Text or PlaceholderInputType.MultilineText)
        {
            if (rules.MinLength.HasValue && valueStr.Length < rules.MinLength.Value)
                return $"Input must be at least {rules.MinLength.Value} characters long.";

            if (rules.MaxLength.HasValue && valueStr.Length > rules.MaxLength.Value)
                return $"Input cannot exceed {rules.MaxLength.Value} characters.";
        }
        
        // Numeric range validation
        if (placeholder.InputType == PlaceholderInputType.Number && value is double numericValue)
        {
            if (rules.Required == true && numericValue == 0) // Basic 'required' check for numbers
                return "A non-zero value is required.";
                
            if (rules.MinValue.HasValue && numericValue < rules.MinValue.Value)
                return $"Value must be at least {rules.MinValue.Value}.";
                
            if (rules.MaxValue.HasValue && numericValue > rules.MaxValue.Value)
                return $"Value cannot exceed {rules.MaxValue.Value}.";
        }

        return null;
    }

    /// <summary>
    /// Asynchronously builds the final, executable Action by substituting all collected values into the instruction prompt.
    /// If a placeholder is of type FilePicker, it reads the content of the file.
    /// </summary>
    /// <returns>A new Action object with the completed instruction.</returns>
    public Task<Action> BuildFinalActionAsync()
    {
        if (_templateAction is null) throw new InvalidOperationException("Template session not started.");

        var finalInstruction = _templateAction.Instruction;
        
        foreach (var placeholder in _templateAction.Placeholders)
        {
            if (!_collectedValues.TryGetValue(placeholder.Name, out var value)) continue;
            var replacementValue = value?.ToString() ?? string.Empty;
            finalInstruction = finalInstruction.Replace($"[{placeholder.Name}]", replacementValue);
        }

        return Task.FromResult(new Action
        {
            Name = _templateAction.Name,
            Instruction = finalInstruction,
            Prefix = _templateAction.Prefix,
            Icon = _templateAction.Icon,
            OutputMode = _templateAction.OutputMode,
            ExplainChanges = _templateAction.ExplainChanges,
            RequiresSelection = _templateAction.RequiresSelection,
            ApplicationContext = _templateAction.ApplicationContext,
            ActionGroupId = _templateAction.ActionGroupId,
            Placeholders = _templateAction.Placeholders
        });
    }
    
    /// <summary>
    /// Finds the next valid step based on display conditions.
    /// </summary>
    private (int Index, ActionPlaceholder Placeholder)? GetNextStep()
    {
        if (_templateAction is null) return null;
        
        var placeholders = _templateAction.Placeholders.ToList();
        for (var i = _currentStepIndex + 1; i < placeholders.Count; i++)
        {
            var candidate = placeholders[i];
            if (IsStepVisible(candidate))
            {
                _currentStepIndex = i;
                return (i, candidate);
            }
        }
        
        _currentStepIndex = -1; // Mark as complete
        return null;
    }

    /// <summary>
    /// Evaluates if a step should be visible based on its DisplayCondition and the currently collected values.
    /// </summary>
    private bool IsStepVisible(ActionPlaceholder placeholder)
    {
        if (string.IsNullOrEmpty(placeholder.DisplayConditionJson)) return true;

        var condition = JsonSerializer.Deserialize<DisplayCondition>(placeholder.DisplayConditionJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (condition is null) return true;

        if (!_collectedValues.TryGetValue(condition.PlaceholderName, out var actualValue)) return false;

        var expectedValue = condition.Value.ToString();
        var actualValueStr = actualValue?.ToString() ?? string.Empty;

        return condition.Operator.ToLowerInvariant() switch
        {
            "equals" => string.Equals(actualValueStr, expectedValue, StringComparison.OrdinalIgnoreCase),
            "notequals" => !string.Equals(actualValueStr, expectedValue, StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }
}