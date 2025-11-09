using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using ProseFlow.Core.Models;
using ProseFlow.UI.Behaviors;
using ProseFlow.UI.ViewModels.Actions;
using ShadUI;
// Required for RoutedEventArgs
using Action = ProseFlow.Core.Models.Action;

namespace ProseFlow.UI.Views.Actions;

public partial class ActionsView : UserControl
{
    public ActionsView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Handles DragOver specifically for the Card element that represents a group.
    /// This determines if the dragged item (an Action) can be dropped onto this group.
    /// </summary>
    private void GroupCard_DragOver(object? sender, DragEventArgs e)
    {
        // Ensure we're dealing with an Action being dragged and a Card as the drop target.
        if (sender is not Card targetCard || !e.Data.Contains(nameof(DataGridDragDropBehavior)))
        {
            e.DragEffects = DragDropEffects.None;
            return;
        }

        // Get the Action being dragged.
        if (e.Data.Get(nameof(DataGridDragDropBehavior)) is not Action draggedAction)
        {
            e.DragEffects = DragDropEffects.None;
            return;
        }

        // Get the target ActionGroup from the Card's DataContext.
        if (targetCard.DataContext is not ActionGroup targetGroup)
        {
            e.DragEffects = DragDropEffects.None;
            return;
        }

        // Prevent dropping an action onto its own group.
        if (draggedAction.ActionGroupId == targetGroup.Id)
        {
            e.DragEffects = DragDropEffects.None;
            return;
        }
        
        e.DragEffects = DragDropEffects.Move;
    }

    /// <summary>
    /// Handles the Drop event for a group Card.
    /// This is where we move an Action from one group to another.
    /// </summary>
    private void GroupCard_Drop(object? sender, DragEventArgs e)
    {
        // Ensure we have all necessary components.
        if (sender is not Card targetCard || DataContext is not ActionsViewModel vm) return;

        // Get the Action being dragged.
        if (e.Data.Get(nameof(DataGridDragDropBehavior)) is not Action draggedAction) return;

        // Get the target ActionGroup from the Card's DataContext.
        if (targetCard.DataContext is not ActionGroup targetGroup) return;
        
        // Prevent dropping an action onto its own group.
        if (draggedAction.ActionGroupId == targetGroup.Id) return;

        // Execute the reorder command.
        var parameter = (dragged: (object)draggedAction, target: (object)targetGroup);
        if (vm.ReorderCommand.CanExecute(parameter))
        {
            vm.ReorderCommand.Execute(parameter);
            e.Handled = true; // Mark the event as handled.
        }
    }

    private async void BulkMoveConfirmButton_OnClick(object? sender, RoutedEventArgs e)
    {
        await Task.Delay(100);
        BulkMoveButton.Flyout?.Hide();
    }
}