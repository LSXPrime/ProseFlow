using System.Windows.Input;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Action = ProseFlow.Core.Models.Action;

namespace ProseFlow.UI.Behaviors;

/// <summary>
/// A single, advanced behavior for handling multiple drag-drop scenarios on a DataGrid:
/// 1. Reordering rows (Actions).
/// 2. Moving rows (Actions) to a new group by dropping on a group header.
/// 3. Reordering groups by dragging group headers.
/// </summary>
public class AdvancedDataGridDropBehavior : AvaloniaObject
{
    private const string ActionDragKey = "DraggableAction";
    private const string GroupDragKey = "DraggableGroupKey";

    public static readonly AttachedProperty<ICommand> CommandProperty =
        AvaloniaProperty.RegisterAttached<AdvancedDataGridDropBehavior, DataGrid, ICommand>(
            "Command", coerce: OnCommandChanged);

    public static ICommand GetCommand(DataGrid element)
    {
        return element.GetValue(CommandProperty);
    }

    public static void SetCommand(DataGrid element, ICommand value)
    {
        element.SetValue(CommandProperty, value);
    }

    private static ICommand OnCommandChanged(AvaloniaObject target, ICommand command)
    {
        if (target is not DataGrid dataGrid) return command;

        dataGrid.SetValue(DragDrop.AllowDropProperty, true);
        dataGrid.AddHandler(DragDrop.DragOverEvent, OnDragOver);
        dataGrid.AddHandler(DragDrop.DropEvent, OnDrop);
        dataGrid.AddHandler(InputElement.PointerPressedEvent, OnPointerPressed, handledEventsToo: true);

        return command;
    }

    private static async void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not DataGrid dataGrid || !e.GetCurrentPoint(dataGrid).Properties.IsLeftButtonPressed ||
            e.Source is not Control control || control.FindAncestorOfType<ICommandSource>(true) is not null) return;

        var dataObject = new DataObject();
        object? dragData = null;

        // Scenario 1: Dragging an Action row
        var row = control.FindAncestorOfType<DataGridRow>();
        if (row?.DataContext is ViewModels.Actions.SelectableActionViewModel actionVm)
        {
            dataObject.Set(ActionDragKey, actionVm);
            dragData = actionVm;
        }
        else // Scenario 2: Dragging a Group header
        {
            var header = control.FindAncestorOfType<DataGridRowGroupHeader>();
            if (header?.DataContext is DataGridCollectionViewGroup { Key: { } groupKey })
            {
                dataObject.Set(GroupDragKey, groupKey);
                dragData = groupKey;
            }
        }

        if (dragData is not null) await DragDrop.DoDragDrop(e, dataObject, DragDropEffects.Move);
    }

    private static void OnDragOver(object? sender, DragEventArgs e)
    {
        // Allow drop if we're dragging either an Action or a Group
        var isActionDrag = e.Data.Contains(ActionDragKey);
        var isGroupDrag = e.Data.Contains(GroupDragKey);

        e.DragEffects = isActionDrag || isGroupDrag ? DragDropEffects.Move : DragDropEffects.None;
    }

    private static void OnDrop(object? sender, DragEventArgs e)
    {
        if (sender is not DataGrid dataGrid) return;

        var command = GetCommand(dataGrid);

        var targetControl = e.Source as Control;
        var targetRow = targetControl?.FindAncestorOfType<DataGridRow>();
        var targetGroupHeader = targetControl?.FindAncestorOfType<DataGridRowGroupHeader>();

        // Get dragged item
        var draggedItem = e.Data.Get(ActionDragKey) ?? e.Data.Get(GroupDragKey);
        if (draggedItem is null) return;

        // Determine drop target and execute command
        object? targetItem = null;
        if (targetRow?.DataContext is not null)
            targetItem = targetRow.DataContext; // Dropped on an Action row
        else if (targetGroupHeader?.DataContext is DataGridCollectionViewGroup { Key: not null } group)
            targetItem = group.Key; // Dropped on a Group header

        if (targetItem is null || ReferenceEquals(draggedItem, targetItem)) return;

        var parameter = (dragged: draggedItem, target: targetItem);
        if (command.CanExecute(parameter)) command.Execute(parameter);
    }
}