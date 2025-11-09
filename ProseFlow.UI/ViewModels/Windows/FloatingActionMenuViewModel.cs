using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ProseFlow.Application.DTOs;
using ProseFlow.Application.Interfaces;
using ProseFlow.Application.Services;
using ProseFlow.Core.Enums;
using ProseFlow.Core.Models;
using ProseFlow.UI.ViewModels.Actions;
using Action = ProseFlow.Core.Models.Action;

namespace ProseFlow.UI.ViewModels.Windows;

public enum MenuState
{
    ActionSelection,
    TemplateFilling
}

public partial class FloatingActionMenuViewModel : ViewModelBase
{
    private readonly TaskCompletionSource<ActionExecutionRequest?> _selectionTcs = new();
    private readonly List<Action> _allAvailableActions;
    private readonly string _activeAppContext;
    private readonly TemplateEngineService _templateEngine;
    private readonly IDocumentReaderService _documentReaderService;

    #region State Machine Properties

    [ObservableProperty] private MenuState _currentMenuState = MenuState.ActionSelection;

    [ObservableProperty]
    private bool _isGenerationMode;

    #endregion

    #region UI-Bound Properties for Action Selection

    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private object? _selectedItem;
    [ObservableProperty] private string _currentServiceTypeName = "Cloud";
    [ObservableProperty] private string _resultContainer = "Default";
    [ObservableProperty] private string _customInstruction = string.Empty;
    [ObservableProperty] private bool _isCustomInstructionActive;
    public ObservableCollection<ActionGroupViewModel> ActionGroups { get; } = [];

    #endregion

    #region UI-Bound Properties for Template Filling

    [ObservableProperty] private TemplateStepViewModelBase? _currentStepViewModel;
    [ObservableProperty] private string _currentStepHeader = string.Empty;
    [ObservableProperty] private string _stepCounterText = string.Empty;
    [ObservableProperty] private bool _isOnFinalStep;
    [ObservableProperty] private bool _canGoBack;
    
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GoToNextStepCommand))]
    private bool _isStepValid = true;

    #endregion

    public event System.Action? RequestClose;
    public bool HasNoActions { get; }

    public FloatingActionMenuViewModel(IEnumerable<Action> availableActions, ProviderSettings providerSettings,
        string activeAppContext, TemplateEngineService templateEngine, IDocumentReaderService documentReaderService,
        bool isGenerationMode)
    {
        _allAvailableActions = availableActions.ToList();
        _activeAppContext = activeAppContext;
        _templateEngine = templateEngine;
        _documentReaderService = documentReaderService;
        IsGenerationMode = isGenerationMode;
        CurrentServiceTypeName = providerSettings.PrimaryServiceType;
        HasNoActions = _allAvailableActions.Count == 0;
        FilterAndGroupActions();
        
        // Subscribe to property changes to manage step validation state
        PropertyChanged += OnViewModelPropertyChanged;
    }

    public Task<ActionExecutionRequest?> WaitForSelectionAsync()
    {
        return _selectionTcs.Task;
    }

    #region Action Selection Logic

    partial void OnSearchTextChanged(string value)
    {
        // In generation mode, the list is static, and the search text is the prompt.
        if (!IsGenerationMode)
            FilterAndGroupActions();
    }

    partial void OnSelectedItemChanged(object? oldValue, object? newValue)
    {
        switch (oldValue)
        {
            // Deselect the old item
            case ActionGroupViewModel oldGroup:
                oldGroup.IsSelected = false;
                break;
            case ActionItemViewModel oldAction:
                oldAction.IsSelected = false;
                break;
        }

        switch (newValue)
        {
            // Select the new item
            case ActionGroupViewModel newGroup:
                newGroup.IsSelected = true;
                break;
            case ActionItemViewModel newAction:
                newAction.IsSelected = true;
                break;
        }
    }

    private void FilterAndGroupActions()
    {
        ActionGroups.Clear();

        // If searching (and not in generation mode), create a flat list under a "Search Results" group.
        if (!string.IsNullOrWhiteSpace(SearchText) && !IsGenerationMode)
        {
            var searchResults = _allAvailableActions
                .Where(a => a.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(a => a.IsFavorite)
                .Select(a => new ActionItemViewModel(a)).ToList();

            if (searchResults.Count != 0)
            {
                var searchGroup = new ActionGroupViewModel("Search Results") { IsExpanded = true };
                foreach (var item in searchResults) searchGroup.Actions.Add(item);
                ActionGroups.Add(searchGroup);
            }
        }
        else // Otherwise (generation mode OR empty search), group actions normally.
        {
            var favoriteActions = _allAvailableActions.Where(a => a.IsFavorite).ToList();
            var nonFavoriteActions = _allAvailableActions.Where(a => !a.IsFavorite).ToList();

            // Create and add the Favorites group if it has any actions
            if (favoriteActions.Count > 0)
            {
                var favoritesGroup = new ActionGroupViewModel("Favorites")
                {
                    IsExpanded = true,
                    IsFavoritesGroup = true
                };

                foreach (var action in favoriteActions.OrderBy(a => a.SortOrder))
                {
                    favoritesGroup.Actions.Add(new ActionItemViewModel(action)
                        { IsContextual = IsActionContextual(action) });
                }

                ActionGroups.Add(favoritesGroup);
            }

            // Group the remaining actions by their actual ActionGroup
            var groupedActions = nonFavoriteActions
                .GroupBy(a => a.ActionGroup)
                .OrderBy(g => g.Key?.SortOrder ?? int.MaxValue);

            foreach (var group in groupedActions)
            {
                var groupName = group.Key?.Name ?? "Uncategorized";
                var actionGroupVm = new ActionGroupViewModel(groupName);

                foreach (var action in group.OrderBy(a => a.SortOrder))
                {
                    actionGroupVm.Actions.Add(new ActionItemViewModel(action)
                        { IsContextual = IsActionContextual(action) });
                }

                ActionGroups.Add(actionGroupVm);
            }
        }

        SelectedItem = GetFlatListOfVisibleItems().FirstOrDefault();
    }

    [RelayCommand]
    private void ConfirmSelection()
    {
        // 1. Prioritize executing a selected action from the list.
        Action? actionToExecute = null;
        if (SelectedItem is ActionItemViewModel actionItem)
        {
            actionToExecute = actionItem.Action;
        }

        if (actionToExecute != null)
        {
            // If we are in generation mode and the user has typed a prompt,
            // intelligently combine it with the selected action.
            if (IsGenerationMode && !string.IsNullOrWhiteSpace(SearchText))
            {
                // Clone the action to avoid modifying the original instance.
                actionToExecute = new Action
                {
                    Id = actionToExecute.Id,
                    Name = actionToExecute.Name,
                    Instruction = actionToExecute.Instruction,
                    Icon = actionToExecute.Icon,
                    OutputMode = actionToExecute.OutputMode,
                    ExplainChanges = actionToExecute.ExplainChanges,
                    RequiresSelection = actionToExecute.RequiresSelection,
                    ApplicationContext = actionToExecute.ApplicationContext,
                    SortOrder = actionToExecute.SortOrder,
                    ActionGroupId = actionToExecute.ActionGroupId,
                    ActionGroup = actionToExecute.ActionGroup,
                    Placeholders = actionToExecute.Placeholders,
                    Prefix = $"{actionToExecute.Prefix} {SearchText}",
                };
            }
            
            // Execute the (potentially modified) action.
            if (actionToExecute.Placeholders.Count != 0)
                StartTemplateFilling(actionToExecute);
            else
                ExecuteSimpleAction(actionToExecute);
            return;
        }

        // 2. If no action is selected, check for generation/custom instruction modes.
        if (IsGenerationMode || IsCustomInstructionActive)
        {
            ExecuteGenerationInstruction();
            return;
        }
        
        // 3. If a group is selected, toggle its expansion.
        if (SelectedItem is ActionGroupViewModel group)
        {
            group.IsExpanded = !group.IsExpanded;
            return;
        }

        // 4. If nothing is actionable, cancel.
        CancelSelection();
    }

    #endregion

    #region Template Filling Logic

    /// <summary>
    /// Initiates the template filling process by starting a session with the TemplateEngineService.
    /// </summary>
    private async void StartTemplateFilling(Action templateAction)
    {
        CurrentMenuState = MenuState.TemplateFilling;

        var firstStep = _templateEngine.StartSession(templateAction);
        if (firstStep is not null)
        {
            await TransitionToStepAsync(firstStep.Value);
        }
        else
        {
            // No valid steps found, execute immediately.
            await ExecuteCompletedTemplateAsync();
        }
    }

    /// <summary>
    /// Creates the ViewModel for a given step and updates the UI state.
    /// </summary>
    private async Task TransitionToStepAsync((int Index, ActionPlaceholder Placeholder) stepInfo)
    {
        var (index, placeholder) = stepInfo;

        var defaultValue = await _templateEngine.ResolveSmartDefaultAsync(placeholder);

        CurrentStepHeader = placeholder.Label;
        StepCounterText = $"(Step {index + 1} of {placeholder.Action?.Placeholders.Count})";
        IsOnFinalStep = index == placeholder.Action?.Placeholders.Count - 1;
        CanGoBack = true;

        CurrentStepViewModel = placeholder.InputType switch
        {
            PlaceholderInputType.Text => new TextStepViewModel(placeholder, defaultValue, _templateEngine),
            PlaceholderInputType.MultilineText => new MultilineTextStepViewModel(placeholder, defaultValue, _templateEngine),
            PlaceholderInputType.Choice => new ChoiceStepViewModel(placeholder, defaultValue),
            PlaceholderInputType.Boolean => new BooleanStepViewModel(placeholder, defaultValue),
            PlaceholderInputType.Number => new NumberStepViewModel(placeholder, defaultValue, _templateEngine),
            PlaceholderInputType.DatePicker => new DatePickerStepViewModel(placeholder, defaultValue),
            PlaceholderInputType.FilePicker => new FilePickerStepViewModel(placeholder, defaultValue, 
                _documentReaderService, _templateEngine),
            _ => throw new NotImplementedException(
                $"No ViewModel is implemented for placeholder type {placeholder.InputType}.")
        };
    }

    /// <summary>
    /// Transitions the UI back to the initial action selection state.
    /// </summary>
    private void TransitionToActionSelection()
    {
        CurrentMenuState = MenuState.ActionSelection;
        CurrentStepViewModel = null;
    }

    private bool CanGoToNextStep() => IsStepValid;

    [RelayCommand(CanExecute = nameof(CanGoToNextStep))]
    private async Task GoToNextStepAsync()
    {
        if (CurrentStepViewModel is null) return;
        
        var value = CurrentStepViewModel.GetValue();

        // Submit the value to the engine.
        var nextStep = _templateEngine.SubmitStep(value);

        // Transition to the next step or execute if finished.
        if (nextStep is not null)
        {
            await TransitionToStepAsync(nextStep.Value);
        }
        else
        {
            await ExecuteCompletedTemplateAsync();
        }
    }

    [RelayCommand]
    private async Task GoToPreviousStepAsync()
    {
        var prevStep = _templateEngine.GetPreviousStep();
        if (prevStep is not null)
        {
            await TransitionToStepAsync(prevStep.Value);
        }
        else
        {
            // We are at the beginning, go back to action selection.
            TransitionToActionSelection();
        }
    }

    [RelayCommand]
    private void SelectChoiceAndProceed(object? choice)
    {
        if (CurrentStepViewModel is not ChoiceStepViewModel vm || choice is not ChoiceOptionViewModel option) return;
        vm.SelectedOption = option;
        GoToNextStepCommand.Execute(null);
    }

    [RelayCommand]
    private void SelectAndConfirmBoolean(object? choice)
    {
        if (CurrentStepViewModel is not BooleanStepViewModel vm || choice is not BooleanOptionViewModel option) return;
        vm.SelectedOption = option;
        GoToNextStepCommand.Execute(null);
    }
    
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CurrentStepViewModel))
        {
            if (CurrentStepViewModel is not null)
            {
                CurrentStepViewModel.PropertyChanged += OnStepViewModelPropertyChanged;
                // Set initial validity for the new step.
                IsStepValid = string.IsNullOrEmpty(CurrentStepViewModel.ErrorMessage);
            }
        }
    }
    
    private void OnStepViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TemplateStepViewModelBase.ErrorMessage))
        {
            // Update the command's executability based on the step's validation status.
            IsStepValid = string.IsNullOrEmpty((sender as TemplateStepViewModelBase)?.ErrorMessage);
        }
    }

    #endregion

    #region Execution and Navigation

    private void ExecuteSimpleAction(Action action)
    {
        var mode = GetCurrentOutputMode();
        var request = new ActionExecutionRequest(action, mode, CurrentServiceTypeName);
        _selectionTcs.TrySetResult(request);
        RequestClose?.Invoke();
    }

    private async Task ExecuteCompletedTemplateAsync()
    {
        var completedAction = await _templateEngine.BuildFinalActionAsync();
        var request = new ActionExecutionRequest(completedAction, GetCurrentOutputMode(), CurrentServiceTypeName);
        _selectionTcs.TrySetResult(request);
        RequestClose?.Invoke();
    }

    private void ExecuteGenerationInstruction()
    {
        var prompt = IsGenerationMode ? SearchText : CustomInstruction;
        if (string.IsNullOrWhiteSpace(prompt)) return;

        var mode = GetCurrentOutputMode() == OutputMode.Default ? OutputMode.InPlace : GetCurrentOutputMode();
        // For generation mode, the user's text becomes the Prefix, which acts as the primary user message to the AI, else it is the instruction.
        var customAction = new Action
        {
            Name = Constants.CustomInstructionActionName ,
            Instruction = IsGenerationMode ? "You are a helpful AI assistant. Respond directly to the user's request." : CustomInstruction,
            Prefix = IsGenerationMode ? prompt : string.Empty,
            OutputMode = mode,
            ExplainChanges = false,
            Icon = "Sparkles",
            RequiresSelection = false
        };

        var request = new ActionExecutionRequest(customAction, mode, CurrentServiceTypeName);
        _selectionTcs.TrySetResult(request);
        RequestClose?.Invoke();
    }

    #endregion

    #region Key Handling Logic

    public bool HandleKeyDown(Key key)
    {
        if (key == Key.Escape)
        {
            CancelSelectionCommand.Execute(null);
            return true;
        }

        return CurrentMenuState switch
        {
            MenuState.ActionSelection => HandleActionSelectionKey(key),
            MenuState.TemplateFilling => HandleTemplateFillingKey(key),
            _ => false
        };
    }

    private bool HandleActionSelectionKey(Key key)
    {
        switch (key)
        {
            case Key.Enter:
                ConfirmSelectionCommand.Execute(null);
                return true;
            case Key.Up:
                SelectPreviousItemCommand.Execute(null);
                return true;
            case Key.Down:
                SelectNextItemCommand.Execute(null);
                return true;
            case Key.Left:
                CollapseSelectedItemCommand.Execute(null);
                return true;
            case Key.Right:
                ExpandSelectedItemCommand.Execute(null);
                return true;
        }

        return false;
    }

    private bool HandleTemplateFillingKey(Key key)
    {
        if (CurrentStepViewModel is ChoiceStepViewModel choiceVm)
        {
            switch (key)
            {
                case Key.Up:
                    choiceVm.SelectPrevious();
                    return true;
                case Key.Down:
                    choiceVm.SelectNext();
                    return true;
            }
        }
        
        if (CurrentStepViewModel is BooleanStepViewModel boolVm)
        {
            switch (key)
            {
                case Key.Up:
                    boolVm.SelectPrevious();
                    return true;
                case Key.Down:
                    boolVm.SelectNext();
                    return true;
            }
        }

        switch (key)
        {
            case Key.Enter:
                if (GoToNextStepCommand.CanExecute(null))
                {
                    GoToNextStepCommand.Execute(null);
                    return true;
                }
                break;

            case Key.Back when CanGoBack: // Check if we can actually go back
                // Only go back if a textbox is empty to avoid interrupting typing.
                if (CurrentStepViewModel is TextStepViewModel textVm && !string.IsNullOrEmpty(textVm.UserInput))
                    return false; // Let the TextBox handle the backspace.
                GoToPreviousStepCommand.Execute(null);
                return true;
        }

        return false;
    }

    #endregion

    #region UI Commands & Helpers

    [RelayCommand]
    private void CancelSelection()
    {
        _selectionTcs.TrySetResult(null);
        RequestClose?.Invoke();
    }

    [RelayCommand]
    private void ToggleServiceType()
    {
        CurrentServiceTypeName = CurrentServiceTypeName == "Cloud" ? "Local" : "Cloud";
    }

    [RelayCommand]
    private void ToggleResultContainer()
    {
        var states = new[] { "Default", "Windowed", "In-place", "Diff" };
        var currentIndex = Array.IndexOf(states, ResultContainer);
        ResultContainer = states[(currentIndex + 1) % states.Length];
    }

    [RelayCommand]
    private void SelectAndConfirmItem(object? item)
    {
        if (item is null) return;
        SelectedItem = item;
        ConfirmSelection();
    }

    [RelayCommand]
    private void SelectNextItem()
    {
        var flatList = GetFlatListOfVisibleItems();
        if (flatList.Count == 0) return;
        var currentIndex = SelectedItem != null ? flatList.IndexOf(SelectedItem) : -1;
        SelectedItem = flatList[(currentIndex + 1) % flatList.Count];
    }

    [RelayCommand]
    private void SelectPreviousItem()
    {
        var flatList = GetFlatListOfVisibleItems();
        if (flatList.Count == 0) return;
        var currentIndex = SelectedItem != null ? flatList.IndexOf(SelectedItem) : -1;
        var newIndex = currentIndex - 1 < 0 ? flatList.Count - 1 : currentIndex - 1;
        SelectedItem = flatList[newIndex];
    }

    [RelayCommand]
    private void CollapseSelectedItem()
    {
        if (SelectedItem is ActionGroupViewModel group)
        {
            group.IsExpanded = false;
        }
        else if (SelectedItem is ActionItemViewModel item)
        {
            var parentGroup = ActionGroups.FirstOrDefault(g => g.Actions.Contains(item));
            if (parentGroup is not null)
            {
                parentGroup.IsExpanded = false;
                SelectedItem = parentGroup; // Move selection to the group header
            }
        }
    }

    [RelayCommand]
    private void ExpandSelectedItem()
    {
        if (SelectedItem is ActionGroupViewModel group) group.IsExpanded = true;
    }

    [RelayCommand]
    private void NavigateToPage(string pageTitle)
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop &&
            desktop.MainWindow?.DataContext is MainViewModel mainWindowViewModel)
        {
            desktop.MainWindow.Show();
            desktop.MainWindow.Activate();
            mainWindowViewModel.Navigate(mainWindowViewModel.PageViewModels.FirstOrDefault(x => x.Title == pageTitle));
        }

        CancelSelection();
    }

    private bool IsActionContextual(Action action)
    {
        return action.ApplicationContext.Count > 0 &&
               action.ApplicationContext.Any(a => a.Contains(_activeAppContext, StringComparison.OrdinalIgnoreCase));
    }

    private List<object> GetFlatListOfVisibleItems()
    {
        var flatList = new List<object>();
        foreach (var group in ActionGroups)
        {
            flatList.Add(group);
            if (group.IsExpanded) flatList.AddRange(group.Actions);
        }

        return flatList;
    }

    private OutputMode GetCurrentOutputMode()
    {
        return ResultContainer switch
        {
            "In-place" => OutputMode.InPlace,
            "Windowed" => OutputMode.Windowed,
            "Diff" => OutputMode.Diff,
            _ => OutputMode.Default
        };
    }

    #endregion
}