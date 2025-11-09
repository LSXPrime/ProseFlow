using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using ProseFlow.Application.Interfaces;
using ProseFlow.Application.Services;
using ProseFlow.Core.Models;

namespace ProseFlow.UI.ViewModels.Actions;

/// <summary>
/// A base class for a single step in the template-filling UI.
/// </summary>
public abstract partial class TemplateStepViewModelBase(ActionPlaceholder placeholder) : ViewModelBase
{
    public ActionPlaceholder Placeholder { get; } = placeholder;

    [ObservableProperty]
    private string? _errorMessage;

    /// <summary>
    /// Gets the value provided by the user for this step.
    /// </summary>
    public abstract object? GetValue();
}

/// <summary>
/// Represents a single selectable option within a Choice step.
/// </summary>
public partial class ChoiceOptionViewModel(string value) : ViewModelBase
{
    public override string Title { get; set; } = $"Choice: {value}";
    public string Value { get; } = value;

    [ObservableProperty]
    private bool _isSelected;
}

/// <summary>
/// Represents a single selectable option ("Yes" or "No") within a Boolean step.
/// </summary>
public partial class BooleanOptionViewModel(string displayText, bool value) : ViewModelBase
{
    public string DisplayText { get; } = displayText;
    public bool Value { get; } = value;

    [ObservableProperty]
    private bool _isSelected;
}

/// <summary>
/// ViewModel for a template step that requires the user to select from a list of choices.
/// </summary>
public partial class ChoiceStepViewModel : TemplateStepViewModelBase
{
    public sealed override string Title { get; set; }
    
    public ObservableCollection<ChoiceOptionViewModel> Options { get; } = [];

    [ObservableProperty]
    private ChoiceOptionViewModel? _selectedOption;

    public ChoiceStepViewModel(ActionPlaceholder placeholder, string? defaultValue) : base(placeholder)
    {
        Title = placeholder.Label;
        var options = JsonSerializer.Deserialize<string[]>(placeholder.OptionsJson) ?? [];
        foreach (var option in options)
        {
            Options.Add(new ChoiceOptionViewModel(option));
        }

        SelectedOption = Options.FirstOrDefault(o => o.Value == defaultValue) ?? Options.FirstOrDefault();
    }
    
    partial void OnSelectedOptionChanged(ChoiceOptionViewModel? oldValue, ChoiceOptionViewModel? newValue)
    {
        if (oldValue is not null) oldValue.IsSelected = false;
        if (newValue is not null) newValue.IsSelected = true;
    }

    public void SelectNext()
    {
        if (Options.Count == 0) return;
        var currentIndex = SelectedOption != null ? Options.IndexOf(SelectedOption) : -1;
        SelectedOption = Options[(currentIndex + 1) % Options.Count];
    }

    public void SelectPrevious()
    {
        if (Options.Count == 0) return;
        var currentIndex = SelectedOption != null ? Options.IndexOf(SelectedOption) : -1;
        var newIndex = currentIndex - 1 < 0 ? Options.Count - 1 : currentIndex - 1;
        SelectedOption = Options[newIndex];
    }

    public override object? GetValue() => SelectedOption?.Value;
}

/// <summary>
/// ViewModel for a template step that requires free-form text input.
/// </summary>
public partial class TextStepViewModel : TemplateStepViewModelBase
{
    private readonly TemplateEngineService _templateEngine;
    public sealed override string Title { get; set; }

    [ObservableProperty]
    private string? _userInput;

    public TextStepViewModel(ActionPlaceholder placeholder, string? defaultValue, TemplateEngineService templateEngine) : base(placeholder)
    {
        _templateEngine = templateEngine;
        Title = placeholder.Label;
        _userInput = defaultValue;
        // Perform initial validation
        ErrorMessage = TemplateEngineService.Validate(Placeholder, _userInput);
    }

    partial void OnUserInputChanged(string? value)
    {
        ErrorMessage = TemplateEngineService.Validate(Placeholder, value);
    }

    public override object? GetValue() => UserInput;
}

/// <summary>
/// ViewModel for a template step that requires a multi-line text input.
/// </summary>
public partial class MultilineTextStepViewModel : TemplateStepViewModelBase
{
    private readonly TemplateEngineService _templateEngine;
    public sealed override string Title { get; set; }

    [ObservableProperty]
    private string? _userInput;

    public MultilineTextStepViewModel(ActionPlaceholder placeholder, string? defaultValue, TemplateEngineService templateEngine) : base(placeholder)
    {
        _templateEngine = templateEngine;
        Title = placeholder.Label;
        _userInput = defaultValue;
        // Perform initial validation
        ErrorMessage = TemplateEngineService.Validate(Placeholder, _userInput);
    }

    partial void OnUserInputChanged(string? value)
    {
        ErrorMessage = TemplateEngineService.Validate(Placeholder, value);
    }

    public override object? GetValue() => UserInput;
}

/// <summary>
/// ViewModel for a template step that requires a boolean (true/false) input.
/// </summary>
public partial class BooleanStepViewModel : TemplateStepViewModelBase
{
    public sealed override string Title { get; set; }
    
    public ObservableCollection<BooleanOptionViewModel> Options { get; } = [];
    
    [ObservableProperty]
    private BooleanOptionViewModel? _selectedOption;

    public BooleanStepViewModel(ActionPlaceholder placeholder, string? defaultValue) : base(placeholder)
    {
        Title = placeholder.Label;
        
        var initialValue = bool.TryParse(defaultValue, out var result) && result;
        
        Options.Add(new BooleanOptionViewModel("Yes", true));
        Options.Add(new BooleanOptionViewModel("No", false));
        
        SelectedOption = Options.FirstOrDefault(o => o.Value == initialValue);
    }
    
    partial void OnSelectedOptionChanged(BooleanOptionViewModel? oldValue, BooleanOptionViewModel? newValue)
    {
        if (oldValue is not null) oldValue.IsSelected = false;
        if (newValue is not null) newValue.IsSelected = true;
    }

    public void SelectNext()
    {
        if (Options.Count == 0) return;
        var currentIndex = SelectedOption != null ? Options.IndexOf(SelectedOption) : -1;
        SelectedOption = Options[(currentIndex + 1) % Options.Count];
    }
    
    public void SelectPrevious()
    {
        if (Options.Count == 0) return;
        var currentIndex = SelectedOption != null ? Options.IndexOf(SelectedOption) : -1;
        var newIndex = currentIndex - 1 < 0 ? Options.Count - 1 : currentIndex - 1;
        SelectedOption = Options[newIndex];
    }

    public override object GetValue() => SelectedOption?.Value ?? false;
}

/// <summary>
/// ViewModel for a template step that requires a numeric input.
/// </summary>
public partial class NumberStepViewModel : TemplateStepViewModelBase
{
    private readonly TemplateEngineService _templateEngine;
    public sealed override string Title { get; set; }

    [ObservableProperty]
    private double _numberValue;
    
    public NumberStepViewModel(ActionPlaceholder placeholder, string? defaultValue, TemplateEngineService templateEngine) : base(placeholder)
    {
        _templateEngine = templateEngine;
        Title = placeholder.Label;
        _numberValue = double.TryParse(defaultValue, out var result) ? result : 0;
        // Perform initial validation
        ErrorMessage = TemplateEngineService.Validate(Placeholder, _numberValue);
    }

    partial void OnNumberValueChanged(double value)
    {
        ErrorMessage = TemplateEngineService.Validate(Placeholder, value);
    }

    public override object GetValue() => NumberValue;
}

/// <summary>
/// ViewModel for a template step that requires a date input.
/// </summary>
public partial class DatePickerStepViewModel(ActionPlaceholder placeholder, string? defaultValue) : TemplateStepViewModelBase(placeholder)
{
    public override string Title { get; set; } = placeholder.Label;

    [ObservableProperty]
    private DateTimeOffset _dateValue = DateTimeOffset.TryParse(defaultValue, out var result) ? result : DateTimeOffset.Now;

    public override object GetValue() => DateValue.Date.ToString("yyyy-MM-dd");
}

/// <summary>
/// ViewModel for a template step that requires a file path input.
/// </summary>
public partial class FilePickerStepViewModel : TemplateStepViewModelBase
{
    public sealed override string Title { get; set; }

    public string Content { get; set; } = string.Empty;
    
    [ObservableProperty]
    private string? _filePath;

    private readonly IDocumentReaderService _documentReaderService;

    /// <summary>
    /// ViewModel for a template step that requires a file path input.
    /// </summary>
    public FilePickerStepViewModel(ActionPlaceholder placeholder,
        string? defaultValue,
        IDocumentReaderService documentReaderService, TemplateEngineService templateEngine) : base(placeholder)
    {
        _documentReaderService = documentReaderService;
        Title = placeholder.Label;
        _filePath = defaultValue;
        // Perform initial validation
        ErrorMessage = TemplateEngineService.Validate(Placeholder, _filePath);
    }


    /// <summary>
    /// Asynchronously validates a file path by attempting to read it, updating the UI with any errors.
    /// </summary>
    /// <param name="newFilePath">The path of the file selected by the user.</param>
    /// <returns>True if the file is valid and readable; otherwise, false.</returns>
    public async Task<bool> ValidateAndSetFileAsync(string newFilePath)
    {
        ErrorMessage = null;
        try
        {
            Content = await _documentReaderService.ReadTextAsync(newFilePath);
            FilePath = newFilePath;
            return true;
        }
        catch (Exception ex)
        {
            Content = string.Empty;
            FilePath = newFilePath; // Show the invalid path to the user for context.
            ErrorMessage = ex.Message; // Display the user-friendly error message.
            return false;
        }
    }

    public override object GetValue() => Content;
}