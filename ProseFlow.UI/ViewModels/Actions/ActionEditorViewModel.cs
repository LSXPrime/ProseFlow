using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ProseFlow.Application.Events;
using ProseFlow.Application.Services;
using ProseFlow.Core.Enums;
using ProseFlow.Core.Models;
using ProseFlow.UI.Services;
using ProseFlow.UI.Utils;
using Action = ProseFlow.Core.Models.Action;

namespace ProseFlow.UI.ViewModels.Actions;

public partial class ActionEditorViewModel : ViewModelBase
{
    private readonly ActionManagementService _actionService;
    private readonly IDialogService _dialogService;
    private readonly bool _isNewAction;

    [ObservableProperty]
    private Action _action;
    
    [ObservableProperty]
    private string _instruction = string.Empty;

    [ObservableProperty]
    private ActionGroup? _selectedActionGroup;

    [ObservableProperty]
    private int _selectedIconTab;

    [ObservableProperty]
    private IconSymbol _selectedLucideIcon;

    [ObservableProperty]
    private string _selectedIcon = string.Empty;

    public List<OutputMode> OutputModes { get; } = Enum.GetValues<OutputMode>().Where(o => o != OutputMode.Default).ToList();
    public List<IconSymbol> LucideIcons { get; } = Enum.GetValues<IconSymbol>().ToList();
    
    public ObservableCollection<ActionGroup> AvailableGroups { get; } = [];
    public ObservableCollection<ActionPlaceholderViewModel> Placeholders { get; } = [];

    public ActionEditorViewModel(Action action, ActionManagementService actionService, IDialogService dialogService)
    {
        _actionService = actionService;
        _dialogService = dialogService;
        _isNewAction = action.Id == 0;

        // Clone the action to avoid modifying the original until save
        _action = new Action
        {
            Id = action.Id,
            Name = action.Name,
            Prefix = action.Prefix,
            Instruction = action.Instruction,
            Icon = action.Icon,
            OutputMode = action.OutputMode,
            ExplainChanges = action.ExplainChanges,
            RequiresSelection = action.RequiresSelection,
            ApplicationContext = [..action.ApplicationContext],
            SortOrder = action.SortOrder,
            ActionGroupId = action.ActionGroupId,
            Placeholders = action.Placeholders.Select(p => new ActionPlaceholder
            {
                Id = p.Id,
                Name = p.Name,
                Label = p.Label,
                InputType = p.InputType,
                OptionsJson = p.OptionsJson,
                DefaultValue = p.DefaultValue,
                ValidationJson = p.ValidationJson,
                DisplayConditionJson = p.DisplayConditionJson,
                ActionId = p.ActionId
            }).ToList()
        };
        
        InitializePlaceholders();
    }

    /// <summary>
    /// Initializes the collection of placeholder ViewModels and wires up their interdependencies.
    /// </summary>
    private void InitializePlaceholders()
    {
        Placeholders.Clear();
        foreach (var placeholder in Action.Placeholders)
        {
            Placeholders.Add(new ActionPlaceholderViewModel(placeholder));
        }

        // Now that all placeholder VMs are created, set up their conditional sources.
        foreach (var placeholderVm in Placeholders)
        {
            // A placeholder can be conditioned on any *other* placeholder.
            var availableSources = Placeholders.Where(p => p != placeholderVm).ToList();
            placeholderVm.AvailableConditionSources.Clear();
            foreach (var source in availableSources)
            {
                placeholderVm.AvailableConditionSources.Add(source);
            }

            // If a condition was loaded, find and select the source placeholder in the ComboBox.
            if (placeholderVm.IsConditional && !string.IsNullOrEmpty(placeholderVm.Placeholder.DisplayConditionJson))
            {
                var condition = JsonSerializer.Deserialize<DisplayCondition>(placeholderVm.Placeholder.DisplayConditionJson);
                if (condition != null)
                {
                    placeholderVm.ConditionSourcePlaceholder = availableSources.FirstOrDefault(p => p.Placeholder.Name == condition.PlaceholderName);
                }
            }
        }
    }

    public override async Task OnNavigatedToAsync()
    {
        // Load available groups for the dropdown
        AvailableGroups.Clear();
        var groups = await _actionService.GetActionGroupsAsync();
        foreach (var group in groups) AvailableGroups.Add(group);

        SelectedActionGroup = AvailableGroups.FirstOrDefault(g => g.Id == Action.ActionGroupId);
        if (SelectedActionGroup is null && AvailableGroups.Count > 0) SelectedActionGroup = AvailableGroups.FirstOrDefault(g => g.Id == 1) ?? AvailableGroups[0];
        
        Instruction = Action.Instruction;

        // Determine the initial state of the icon selection
        if (Enum.TryParse<IconSymbol>(Action.Icon, true, out var kind))
        {
            SelectedLucideIcon = kind;
            SelectedIcon = kind.ToString();
            SelectedIconTab = 0; // "Built-in" tab
        }
        else if (!string.IsNullOrEmpty(Action.Icon))
        {
            SelectedLucideIcon = IconSymbol.Workflow;
            SelectedIcon = Action.Icon;
            SelectedIconTab = 1; // "Custom" tab
        }
    }
    
    partial void OnInstructionChanged(string value)
    {
        Action.Instruction = value;
    }
    
    partial void OnSelectedActionGroupChanged(ActionGroup? value)
    {
        if (value is null) return;
        Action.ActionGroupId = value.Id;
    }

    partial void OnSelectedIconChanged(string value)
    { 
        Action.Icon = value;
    }

    partial void OnSelectedLucideIconChanged(IconSymbol value)
    {
        Action.Icon = value.ToString();
    }
    
    // When the user switches tabs, ensure the Action model reflects the right source.
    partial void OnSelectedIconTabChanged(int value)
    {
        Action.Icon = value == 0 ? SelectedLucideIcon.ToString() : SelectedIcon;
    }

    [RelayCommand]
    private async Task AddPlaceholderAsync()
    {
        var result = await _dialogService.ShowInputDialogAsync("Add Placeholder", "Enter a unique name for the placeholder (e.g., 'language', 'tone').", "Add");
        if (!result.Success || string.IsNullOrWhiteSpace(result.Text)) return;

        var name = result.Text.Trim();
        if (Placeholders.Any(p => p.Placeholder.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
        {
            AppEvents.RequestNotification("A placeholder with that name already exists.", NotificationType.Warning);
            return;
        }

        var newPlaceholder = new ActionPlaceholder { Name = name, Label = name };
        Action.Placeholders.Add(newPlaceholder);
        
        // Re-initialize the entire placeholder list to correctly update dependencies
        InitializePlaceholders();
    }

    [RelayCommand]
    private void RemovePlaceholder(ActionPlaceholderViewModel placeholderVm)
    {
        var model = placeholderVm.Placeholder;
        Action.Placeholders.Remove(model);
        
        // Re-initialize the entire placeholder list to correctly update dependencies
        InitializePlaceholders();
    }

    /// <summary>
    /// Inserts the placeholder's text representation into the instruction at the given caret index.
    /// This method is called from the view's code-behind.
    /// </summary>
    public void InsertPlaceholderText(string placeholderName, int caretIndex)
    {
        var textToInsert = $"[{placeholderName}]";
        Instruction = Instruction.Insert(caretIndex, textToInsert);
    }

    public async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(Action.Name))
        {
            AppEvents.RequestNotification("Please provide a name for the action.", NotificationType.Warning);
            return;
        }

        if (Action.ActionGroupId == 0)
        {
            AppEvents.RequestNotification("Please select a group for the action.", NotificationType.Warning);
            return;
        }

        // Map the placeholder VMs back to the action model's collection before saving
        Action.Placeholders = Placeholders.Select(p => p.Placeholder).ToList();

        if (_isNewAction)
            await _actionService.CreateActionAsync(Action);
        else
            await _actionService.UpdateActionAsync(Action);
    }
}