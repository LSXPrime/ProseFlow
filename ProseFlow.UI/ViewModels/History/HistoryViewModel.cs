using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ProseFlow.Application.Services;
using ProseFlow.Core.Enums;
using ProseFlow.Core.Models;
using ProseFlow.UI.Services;
using ProseFlow.UI.Utils;
using Action = ProseFlow.Core.Models.Action;

namespace ProseFlow.UI.ViewModels.History;

public partial class HistoryViewModel(
    HistoryService historyService,
    IDialogService dialogService) : ViewModelBase
{
    public override string Title => "History";
    public override IconSymbol Icon => IconSymbol.History;

    private CancellationTokenSource _searchDebounceCts = new();

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _selectedFilter = "All";
    
    [ObservableProperty]
    private string _emptyStateMessage = "Your recent interactions will appear here.";

    public List<string> AvailableFilters { get; } = ["All", "Action Name", "Input", "Output"];
    public ObservableCollection<HistoryEntry> HistoryEntries { get; } = [];
    public bool HasHistory => HistoryEntries.Any();

    public override async Task OnNavigatedToAsync()
    {
        await LoadHistoryAsync();
    }
    
    partial void OnSearchTextChanged(string value) => DebounceSearch();
    partial void OnSelectedFilterChanged(string value) => DebounceSearch(true);

    private async void DebounceSearch(bool immediate = false)
    {
        try
        {
            await _searchDebounceCts.CancelAsync();
            _searchDebounceCts = new CancellationTokenSource();
            if (!immediate) 
                await Task.Delay(300, _searchDebounceCts.Token);
            
            await LoadHistoryAsync();
        }
        catch (TaskCanceledException)
        {
            // This is expected when the user types quickly, do nothing.
        }
    }

    private async Task LoadHistoryAsync()
    {
        HistoryEntries.Clear();
        var entries = await historyService.GetHistoryAsync(SearchText, SelectedFilter);
        foreach (var entry in entries) HistoryEntries.Add(entry);
        
        OnPropertyChanged(nameof(HasHistory));
        UpdateEmptyStateMessage();
    }
    
    private void UpdateEmptyStateMessage()
    {
        if (!HistoryEntries.Any() && !string.IsNullOrWhiteSpace(SearchText))
            EmptyStateMessage = "No search results found.";
        else
            EmptyStateMessage = "Your recent interactions will appear here.";
    }

    [RelayCommand]
    private async Task SaveHistoryEntryAsActionAsync(HistoryEntry? entry)
    {
        if (entry is null || entry.ActionName != Constants.CustomInstructionActionName) return;

        var newAction = new Action
        {
            Name = "",
            Instruction = entry.Instruction,
            Prefix = string.Empty,
            Icon = nameof(IconSymbol.Workflow),
            OutputMode = OutputMode.InPlace,
            ExplainChanges = false,
            RequiresSelection = false,
            ActionGroupId = 1, // Default "General" group.
            Placeholders = new List<ActionPlaceholder>()
        };

        await dialogService.ShowActionEditorDialogAsync(newAction);
    }
    
    [RelayCommand]
    private void DeleteHistoryEntry(HistoryEntry entry)
    {
        dialogService.ShowConfirmationDialogAsync("Delete History Entry", "Are you sure you want to delete this history entry?", async () =>
        {
            await historyService.DeleteHistoryEntryAsync(entry);
            await LoadHistoryAsync();
        });
    }

    [RelayCommand]
    private void ClearHistory()
    {
        dialogService.ShowConfirmationDialogAsync("Clear History", "Are you sure you want to clear the history?",
            async () =>
            {
                await historyService.ClearHistoryAsync();
                await LoadHistoryAsync();
            });
    }
}