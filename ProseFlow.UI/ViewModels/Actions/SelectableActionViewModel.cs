using CommunityToolkit.Mvvm.ComponentModel;
using Action = ProseFlow.Core.Models.Action;

namespace ProseFlow.UI.ViewModels.Actions;

/// <summary>
/// A ViewModel that wraps an Action entity to add UI-specific properties, like selection state.
/// </summary>
public partial class SelectableActionViewModel(Action action) : ViewModelBase
{
    /// <summary>
    /// The underlying Action data model.
    /// </summary>
    public Action Action { get; } = action;

    [ObservableProperty]
    private bool _isSelected;
}