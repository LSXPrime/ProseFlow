using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ProseFlow.Core.Enums;
using ProseFlow.Core.Models;

namespace ProseFlow.UI.ViewModels.Actions;

// Internal records for clean JSON serialization/deserialization.
internal record ValidationRules(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool Required,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] int? MinLength,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] int? MaxLength,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] double? MinValue,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] double? MaxValue);

internal record DisplayCondition(string PlaceholderName, string Operator, string Value);

/// <summary>
/// A ViewModel wrapping an ActionPlaceholder entity for use in the ActionEditor.
/// Manages advanced features like validation and conditional display.
/// </summary>
public partial class ActionPlaceholderViewModel : ViewModelBase
{
    [ObservableProperty]
    private ActionPlaceholder _placeholder;

    [ObservableProperty]
    private string _newOptionText = string.Empty;

    [ObservableProperty]
    private PlaceholderInputType _inputType;

    public ObservableCollection<string> Options { get; } = [];
    public List<PlaceholderInputType> AvailableInputTypes => Enum.GetValues<PlaceholderInputType>().ToList();
    public IReadOnlyList<SmartTokenInfo> AvailableSmartTokens => SmartTokens.All;

    #region Validation Properties
    [ObservableProperty] private bool _isRequired;
    [ObservableProperty] private int? _minLength;
    [ObservableProperty] private int? _maxLength;
    [ObservableProperty] private double? _minValue;
    [ObservableProperty] private double? _maxValue;
    public bool IsTextualInput => InputType is PlaceholderInputType.Text or PlaceholderInputType.MultilineText;
    public bool IsNumericInput => InputType == PlaceholderInputType.Number;
    #endregion

    #region Conditional Display Properties
    [ObservableProperty] private bool _isConditional;
    [ObservableProperty] private ActionPlaceholderViewModel? _conditionSourcePlaceholder;
    [ObservableProperty] private string _conditionOperator = "equals";
    [ObservableProperty] private string _conditionValue = string.Empty;
    public List<string> AvailableOperators { get; } = ["equals", "notEquals"];
    public ObservableCollection<ActionPlaceholderViewModel> AvailableConditionSources { get; } = [];
    #endregion
    
    #region Default Value Properties

    /// <summary>
    /// Gets a value indicating whether the text-based default value input should be visible.
    /// </summary>
    public bool IsDefaultValueTextVisible => InputType is PlaceholderInputType.Text or PlaceholderInputType.MultilineText;
    
    /// <summary>
    /// Gets a value indicating whether the number-based default value input should be visible.
    /// </summary>
    public bool IsDefaultValueNumberVisible => InputType == PlaceholderInputType.Number;

    /// <summary>
    /// Gets a value indicating whether the boolean toggle for the default value should be visible.
    /// </summary>
    public bool IsDefaultValueBooleanVisible => InputType == PlaceholderInputType.Boolean;
    
    /// <summary>
    /// Gets a value indicating whether the date picker for the default value should be visible.
    /// </summary>
    public bool IsDefaultValueDateVisible => InputType == PlaceholderInputType.DatePicker;

    /// <summary>
    /// A proxy property to handle binding the boolean default value to a ToggleSwitch.
    /// </summary>
    public bool DefaultBooleanValue
    {
        get => bool.TryParse(Placeholder.DefaultValue, out var result) && result;
        set => Placeholder.DefaultValue = value.ToString();
    }
    
    /// <summary>
    /// A proxy property to handle binding the date default value to a DatePicker.
    /// </summary>
    public DateTimeOffset? DefaultDateValue
    {
        get => DateTimeOffset.TryParse(Placeholder.DefaultValue, out var result) ? result : null;
        set => Placeholder.DefaultValue = value?.ToString("o"); // ISO 8601 format
    }
    
    #endregion

    public ActionPlaceholderViewModel(ActionPlaceholder placeholder)
    {
        _placeholder = placeholder;
        LoadStateFromJson();
        
        // When any sub-property changes, rebuild the JSON properties in the underlying model.
        PropertyChanged += OnSubPropertyChanged;
    }

    /// <summary>
    /// Populates the ViewModel's simple properties by deserializing the model's JSON strings.
    /// </summary>
    private void LoadStateFromJson()
    {
        // Set InputType first as other properties depend on it
        InputType = Placeholder.InputType;
        
        // Load Options for Choice type
        Options.Clear();
        if (!string.IsNullOrWhiteSpace(Placeholder.OptionsJson))
        {
            var options = JsonSerializer.Deserialize<List<string>>(Placeholder.OptionsJson);
            if (options is not null)
            {
                foreach (var option in options) Options.Add(option);
            }
        }

        // Load Validation Rules
        if (!string.IsNullOrEmpty(Placeholder.ValidationJson))
        {
            try
            {
                var rules = JsonSerializer.Deserialize<ValidationRules>(Placeholder.ValidationJson);
                if (rules is not null)
                {
                    IsRequired = rules.Required;
                    MinLength = rules.MinLength;
                    MaxLength = rules.MaxLength;
                    MinValue = rules.MinValue;
                    MaxValue = rules.MaxValue;
                }
            }
            catch { /* Ignore deserialization errors for robustness */ }
        }

        // Load Conditional Display Rule
        if (!string.IsNullOrEmpty(Placeholder.DisplayConditionJson))
        {
            try
            {
                var condition = JsonSerializer.Deserialize<DisplayCondition>(Placeholder.DisplayConditionJson);
                if (condition != null)
                {
                    IsConditional = true;
                    // Note: ConditionSourcePlaceholder is set externally by the parent ActionEditorViewModel
                    ConditionOperator = condition.Operator;
                    ConditionValue = condition.Value;
                }
            }
            catch { /* Ignore deserialization errors */ }
        }
    }

    /// <summary>
    /// Serializes the ViewModel's state back into the model's JSON properties.
    /// </summary>
    private void SaveStateToJson()
    {
        // Save Options for Choice type
        Placeholder.OptionsJson = JsonSerializer.Serialize(Options);
        if (!Options.Contains(Placeholder.DefaultValue ?? string.Empty))
        {
            Placeholder.DefaultValue = Options.FirstOrDefault();
        }

        // Save Validation Rules
        var rules = new ValidationRules(
            IsRequired,
            MinLength,
            MaxLength,
            MinValue,
            MaxValue
        );
        Placeholder.ValidationJson = JsonSerializer.Serialize(rules, new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull });

        // Save Conditional Display Rule
        if (IsConditional && ConditionSourcePlaceholder != null && !string.IsNullOrWhiteSpace(ConditionOperator))
        {
            var condition = new DisplayCondition(ConditionSourcePlaceholder.Placeholder.Name, ConditionOperator, ConditionValue);
            Placeholder.DisplayConditionJson = JsonSerializer.Serialize(condition);
        }
        else
        {
            Placeholder.DisplayConditionJson = null; // Clear the condition if it's disabled
        }
    }
    
    /// <summary>
    /// Handles property changes to trigger JSON serialization and related UI updates.
    /// </summary>
    private void OnSubPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(InputType))
        {
            Placeholder.InputType = InputType;
            OnPropertyChanged(nameof(IsTextualInput)); // Update visibility of length fields
            OnPropertyChanged(nameof(IsNumericInput)); // Update visibility of value range fields
            
            // Notify that visibility properties for default value controls have changed
            OnPropertyChanged(nameof(IsDefaultValueTextVisible));
            OnPropertyChanged(nameof(IsDefaultValueNumberVisible));
            OnPropertyChanged(nameof(IsDefaultValueBooleanVisible));
            OnPropertyChanged(nameof(IsDefaultValueDateVisible));
        }

        // Any change to a rule-related property should trigger a save.
        var isRuleProperty = e.PropertyName is nameof(IsRequired) or nameof(MinLength) or nameof(MaxLength) or
                                              nameof(MinValue) or nameof(MaxValue) or
                                              nameof(IsConditional) or nameof(ConditionSourcePlaceholder) or
                                              nameof(ConditionOperator) or nameof(ConditionValue);
        if (isRuleProperty)
        {
            SaveStateToJson();
        }
    }

    [RelayCommand]
    private void AddOption()
    {
        if (string.IsNullOrWhiteSpace(NewOptionText) || Options.Contains(NewOptionText))
        {
            NewOptionText = string.Empty;
            return;
        }

        Options.Add(NewOptionText);
        NewOptionText = string.Empty;
        SaveStateToJson();
    }

    [RelayCommand]
    private void RemoveOption(string option)
    {
        if (!Options.Contains(option)) return;
        
        Options.Remove(option);
        SaveStateToJson();
    }
}