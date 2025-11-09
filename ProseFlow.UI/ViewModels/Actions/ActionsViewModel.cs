using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Collections;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ProseFlow.UI.Utils;
using ProseFlow.Application.Events;
using ProseFlow.Application.Services;
using ProseFlow.Core.Models;
using ProseFlow.UI.Services;
using Action = ProseFlow.Core.Models.Action;

namespace ProseFlow.UI.ViewModels.Actions;

public partial class ActionsViewModel(
    ActionManagementService actionService,
    IDialogService dialogService) : ViewModelBase
{
    public override string Title => "Actions";
    public override IconSymbol Icon => IconSymbol.Workflow;

    private List<ActionGroup> _actionGroupsList = [];
    private readonly ObservableCollection<SelectableActionViewModel> _allActions = [];

    [ObservableProperty]
    private DataGridCollectionView? _groupedActions;

    [ObservableProperty]
    private int _selectedItemsCount;

    [ObservableProperty]
    private bool _isAnyItemSelected;

    [ObservableProperty]
    private bool _isAllSelected;

    public ObservableCollection<ActionGroup> AvailableGroups { get; } = [];
    public bool HasActions => _allActions.Any();

    public override async Task OnNavigatedToAsync()
    {
        await LoadDataAsync();
    }

    private async Task LoadDataAsync()
    {
        foreach (var selectableVm in _allActions) selectableVm.PropertyChanged -= OnItemSelectionChanged;
        _allActions.Clear();
        AvailableGroups.Clear();
        
        var groups = await actionService.GetActionGroupsWithActionsAsync();
        _actionGroupsList = groups.OrderBy(g => g.SortOrder).ToList();
        foreach (var group in _actionGroupsList) AvailableGroups.Add(group);
        
        var sortedActions = _actionGroupsList
            .SelectMany(g => g.Actions)
            .OrderBy(a => a.ActionGroup!.SortOrder)
            .ThenBy(a => a.SortOrder);

        foreach (var action in sortedActions)
        {
            var selectableVm = new SelectableActionViewModel(action);
            selectableVm.PropertyChanged += OnItemSelectionChanged;
            _allActions.Add(selectableVm);
        }

        var collectionView = new DataGridCollectionView(_allActions);
        collectionView.GroupDescriptions.Add(new DataGridPathGroupDescription("Action.ActionGroup.Name"));
        
        GroupedActions = collectionView;
        UpdateSelectionState();
        OnPropertyChanged(nameof(HasActions));
    }

    private void OnItemSelectionChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SelectableActionViewModel.IsSelected)) UpdateSelectionState();
    }

    private void UpdateSelectionState()
    {
        SelectedItemsCount = _allActions.Count(a => a.IsSelected);
        IsAnyItemSelected = SelectedItemsCount > 0;
    }
    
    partial void OnIsAllSelectedChanged(bool value)
    {
        foreach (var item in _allActions) item.IsSelected = value;
    }

    #region Single Action Commands

    [RelayCommand]
    private async Task AddActionAsync()
    {
        var newAction = new Action { Name = "New Action" };
        await dialogService.ShowActionEditorDialogAsync(newAction);
        await LoadDataAsync();
    }
    
    [RelayCommand]
    private async Task AddGroupAsync()
    {
        var result = await dialogService.ShowInputDialogAsync(
            "Create New Group",
            "Enter a name for the new group:",
            "Create");
        
        if (result.Success && !string.IsNullOrWhiteSpace(result.Text))
        {
            await actionService.CreateActionGroupAsync(new ActionGroup { Name = result.Text });
            await LoadDataAsync();
        }
    }

    [RelayCommand]
    private async Task EditActionAsync(SelectableActionViewModel? actionVm)
    {
        if (actionVm is null) return;
        await dialogService.ShowActionEditorDialogAsync(actionVm.Action);
        await LoadDataAsync();
    }
    
    [RelayCommand]
    private async Task EditGroupAsync(object? groupKey)
    {
        if (groupKey is not string groupName || string.IsNullOrWhiteSpace(groupName)) return;

        var group = _actionGroupsList.FirstOrDefault(g => g.Name == groupName);
        if (group is null) return;

        var result = await dialogService.ShowInputDialogAsync(
            "Rename Group",
            $"Enter a new name for '{group.Name}':",
            "Rename",
            group.Name);
        
        if (result.Success && !string.IsNullOrWhiteSpace(result.Text))
        {
            group.Name = result.Text;
            await actionService.UpdateActionGroupAsync(group);
            await LoadDataAsync();
        }
    }

    [RelayCommand]
    private void DeleteAction(SelectableActionViewModel? actionVm)
    {
        if (actionVm is null) return;
        dialogService.ShowConfirmationDialogAsync(
            "Delete Action",
            $"Are you sure you want to delete the action '{actionVm.Action.Name}'?", async () =>
            {
                await actionService.DeleteActionAsync(actionVm.Action.Id);
                await LoadDataAsync();
            });
    }
    
    [RelayCommand]
    private void DeleteGroup(object? groupKey)
    {
        if (groupKey is not string groupName || string.IsNullOrWhiteSpace(groupName)) return;
        
        var group = _actionGroupsList.FirstOrDefault(g => g.Name == groupName);
        if (group is null) return;

        if (group.Id == 1)
        {
            AppEvents.RequestNotification("The default 'General' group cannot be deleted.", NotificationType.Warning);
            return;
        }

        dialogService.ShowConfirmationDialogAsync(
            $"Delete '{group.Name}' Group?",
            "The actions inside this group will NOT be deleted. They will be moved to the 'General' group.", async () =>
            {
                await actionService.DeleteActionGroupAsync(group.Id);
                await LoadDataAsync();
            });
    }
    
    [RelayCommand]
    private async Task ToggleFavoriteAsync(SelectableActionViewModel? actionVm)
    {
        if (actionVm is null) return;
        
        await actionService.ToggleFavoriteAsync(actionVm.Action.Id);
        
        var actionInList = _allActions.FirstOrDefault(a => a.Action.Id == actionVm.Action.Id);
        if (actionInList is not null)
        {
            actionInList.Action.IsFavorite = !actionInList.Action.IsFavorite;
            GroupedActions?.Refresh();
        }
    }
    
    [RelayCommand]
    private async Task DuplicateActionAsync(SelectableActionViewModel? actionVm)
    {
        if (actionVm is null) return;
        await actionService.DuplicateActionAsync(actionVm.Action.Id);
        await LoadDataAsync();
    }

    private bool CanMoveAction(SelectableActionViewModel? actionVm, int direction)
    {
        if (actionVm is null) return false;
        var groupActions = _allActions.Where(a => a.Action.ActionGroupId == actionVm.Action.ActionGroupId).ToList();
        var index = groupActions.IndexOf(actionVm);
        return direction switch
        {
            -1 => index > 0, // Can move up if not the first
            1 => index < groupActions.Count - 1, // Can move down if not the last
            _ => false
        };
    }
    
    [RelayCommand(CanExecute = nameof(CanMoveUp))]
    private async Task MoveActionUpAsync(SelectableActionViewModel? actionVm)
    {
        if (actionVm is null) return;
        var groupActions = _allActions.Where(a => a.Action.ActionGroupId == actionVm.Action.ActionGroupId).ToList();
        var currentIndex = groupActions.IndexOf(actionVm);
        await actionService.UpdateActionOrderAsync(actionVm.Action.Id, actionVm.Action.ActionGroupId, currentIndex - 1);
        await LoadDataAsync();
    }
    
    [RelayCommand(CanExecute = nameof(CanMoveDown))]
    private async Task MoveActionDownAsync(SelectableActionViewModel? actionVm)
    {
        if (actionVm is null) return;
        var groupActions = _allActions.Where(a => a.Action.ActionGroupId == actionVm.Action.ActionGroupId).ToList();
        var currentIndex = groupActions.IndexOf(actionVm);
        await actionService.UpdateActionOrderAsync(actionVm.Action.Id, actionVm.Action.ActionGroupId, currentIndex + 1);
        await LoadDataAsync();
    }

    private bool CanMoveUp(SelectableActionViewModel? actionVm) => CanMoveAction(actionVm, -1);
    private bool CanMoveDown(SelectableActionViewModel? actionVm) => CanMoveAction(actionVm, 1);

    #endregion
    
    #region Bulk Action Commands

    [RelayCommand]
    private void ToggleGroupSelection(object? groupObject)
    {
        if (groupObject is not DataGridCollectionViewGroup group) return;

        // If any item is not selected, select all, Otherwise (if all are already selected), deselect all.
        var targetState = group.Items.Cast<SelectableActionViewModel>().Any(vm => !vm.IsSelected);

        foreach (var item in group.Items)
        {
            if (item is SelectableActionViewModel actionVm) actionVm.IsSelected = targetState;
        }
    }
    
    [RelayCommand]
    private void BulkDelete()
    {
        var selectedActionNames = _allActions.Where(a => a.IsSelected).Select(a => a.Action.Name);
        dialogService.ShowConfirmationDialogAsync(
            $"Delete {SelectedItemsCount} Actions?",
            $"Are you sure you want to permanently delete the selected actions? This cannot be undone.\n\n- {string.Join("\n- ", selectedActionNames)}",
            async () =>
            {
                var idsToDelete = _allActions.Where(a => a.IsSelected).Select(a => a.Action.Id).ToList();
                await actionService.DeleteActionsAsync(idsToDelete);
                await LoadDataAsync();
            });
    }
    
    [RelayCommand]
    private async Task ConfirmBulkMoveAsync(ActionGroup targetGroup)
    {
        var idsToMove = _allActions.Where(a => a.IsSelected).Select(a => a.Action.Id).ToList();
        await actionService.MoveActionsToGroupAsync(idsToMove, targetGroup.Id);
        
        await LoadDataAsync();
    }
    
    [RelayCommand]
    private async Task BulkExport()
    {
        if (!IsAnyItemSelected) return;

        var selectedIds = _allActions.Where(a => a.IsSelected).Select(a => a.Action.Id).ToList();
        var filePath = await dialogService.ShowSaveFileDialogAsync("Export Selected Actions", "proseflow_selection.json", "JSON files", "*.json");

        if (string.IsNullOrWhiteSpace(filePath)) return;

        try
        {
            await actionService.ExportActionsToJsonAsync(selectedIds, filePath);
            AppEvents.RequestNotification($"{selectedIds.Count} actions exported successfully.", NotificationType.Success);
        }
        catch (Exception)
        {
            AppEvents.RequestNotification("Failed to export selected actions.", NotificationType.Error);
        }
    }

    #endregion

    #region Reorder Command

    [RelayCommand]
    private async Task ReorderAsync((object dragged, object target) items)
    {
        switch (items)
        {
            // Case 1: An Action is dropped onto another Action
            case { dragged: SelectableActionViewModel draggedVm, target: SelectableActionViewModel targetVm }:
            {
                var draggedAction = draggedVm.Action;
                var targetAction = targetVm.Action;
                
                var group = _actionGroupsList.FirstOrDefault(g => g.Id == targetAction.ActionGroupId);
                var newIndex = group?.Actions.OrderBy(a => a.SortOrder).ToList().FindIndex(a => a.Id == targetAction.Id) ?? 0;
                
                await actionService.UpdateActionOrderAsync(draggedAction.Id, targetAction.ActionGroupId, newIndex);
                break;
            }

            // Case 2: An Action is dropped onto a Group Header (string)
            case { dragged: SelectableActionViewModel draggedVm, target: string targetGroupName }:
            {
                var draggedAction = draggedVm.Action;
                var targetGroup = _actionGroupsList.FirstOrDefault(g => g.Name == targetGroupName);
                if (targetGroup is null || draggedAction.ActionGroupId == targetGroup.Id) return;

                // Move to the top of the new group
                await actionService.UpdateActionOrderAsync(draggedAction.Id, targetGroup.Id, 0);
                break;
            }

            // Case 3: A Group Header (string) is dropped onto another Group Header (string)
            case { dragged: string draggedGroupName, target: string targetGroupName }:
            {
                var orderedGroups = new ObservableCollection<ActionGroup>(_actionGroupsList);
                var draggedGroup = orderedGroups.FirstOrDefault(g => g.Name == draggedGroupName);
                var targetGroup = orderedGroups.FirstOrDefault(g => g.Name == targetGroupName);

                if (draggedGroup is null || targetGroup is null) return;

                var oldIndex = orderedGroups.IndexOf(draggedGroup);
                var newIndex = orderedGroups.IndexOf(targetGroup);
                
                if (oldIndex == -1 || newIndex == -1) return;
                
                orderedGroups.Move(oldIndex, newIndex);

                await actionService.UpdateActionGroupOrderAsync(orderedGroups.ToList());
                break;
            }
        }
        
        await LoadDataAsync();
    }
    
    #endregion
    
    #region Import/Export
    
    [RelayCommand]
    private async Task ImportActionsAsync()
    {
        var filePath = await dialogService.ShowOpenFileDialogAsync("Import Actions", "JSON files", "*.json");
        if (string.IsNullOrWhiteSpace(filePath)) return;

        try
        {
            await actionService.ImportActionsFromJsonAsync(filePath);
            await LoadDataAsync();
            AppEvents.RequestNotification("Actions imported successfully.", NotificationType.Success);
        }
        catch (Exception)
        {
            AppEvents.RequestNotification("Failed to import actions.", NotificationType.Error);
        }
    }

    [RelayCommand]
    private async Task ExportActionsAsync()
    {
        var filePath =
            await dialogService.ShowSaveFileDialogAsync("Export All Actions", "proseflow_actions.json", "JSON files",
                "*.json");
        if (string.IsNullOrWhiteSpace(filePath)) return;

        try
        {
            await actionService.ExportActionsToJsonAsync(filePath);
            AppEvents.RequestNotification("All actions exported successfully.", NotificationType.Success);
        }
        catch (Exception)
        {
            AppEvents.RequestNotification("Failed to export actions.", NotificationType.Error);
        }
    }
    
    #endregion
}