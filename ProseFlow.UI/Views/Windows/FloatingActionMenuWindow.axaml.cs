using System;
using System.ComponentModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.DependencyInjection;
using ProseFlow.Core.Models;
using ProseFlow.UI.Services;
using ProseFlow.UI.ViewModels.Actions;
using ProseFlow.UI.ViewModels.Windows;
using Window = ShadUI.Window;

namespace ProseFlow.UI.Views.Windows;

public partial class FloatingActionMenuWindow : Window
{
    #region Fields

    private ItemsControl? _currentListOptionsControl;
    private INotifyPropertyChanged? _currentListStepViewModel;
    
    private bool _isPreventClose;

    #endregion

    public FloatingActionMenuWindow()
    {
        InitializeComponent();
    }

    #region Window Lifecycle Handlers

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (DataContext is FloatingActionMenuViewModel oldVm)
        {
            oldVm.RequestClose -= OnRequestClose;
            oldVm.PropertyChanged -= OnViewModelPropertyChanged;
        }

        if (DataContext is FloatingActionMenuViewModel newVm)
        {
            newVm.RequestClose += OnRequestClose;
            newVm.PropertyChanged += OnViewModelPropertyChanged;
        }
    }

    private void OnWindowOpened(object? sender, EventArgs e)
    {
        // Position the window near the mouse cursor
        if (Screens.Primary != null)
            Position = new PixelPoint(
                (int)(Screens.Primary.WorkingArea.Center.X - Width / 2),
                (int)(Screens.Primary.WorkingArea.Center.Y - Height / 2 - 100)
            );

        // Focus the search box for immediate typing
        Dispatcher.UIThread.Post(() =>
        {
            Activate();
            Focus();
            PrimaryInputBox.Focus();
        }, DispatcherPriority.Background);
    }

    private void Window_OnDeactivated(object? sender, EventArgs e)
    {
        if (_isPreventClose) return;
        // When the user clicks away from the window, treat it as a cancellation and request closing.
        if (DataContext is FloatingActionMenuViewModel vm)
            vm.CancelSelectionCommand.Execute(null);
        else // Fallback if the ViewModel is not available for some reason.
            Close();
    }

    #endregion

    #region Input Event Handlers

    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not FloatingActionMenuViewModel vm) return;
        
        e.Handled = vm.HandleKeyDown(e.Key);
    }

    private void ActionButton_OnPointerPressed(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: ActionItemViewModel actionVm } ||
            DataContext is not FloatingActionMenuViewModel vm) return;

        vm.SelectAndConfirmItemCommand.Execute(actionVm);
    }

    private void CustomInstructionButton_OnPointerPressed(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not FloatingActionMenuViewModel vm) return;
        vm.ConfirmSelectionCommand.Execute(null);
    }
    
    private void MultilineTextBox_OnKeyDownHandler(object? sender, KeyEventArgs e)
    {
        Console.WriteLine($"Key: {e.Key}, Modifiers: {e.KeyModifiers}");
        // Check for the Ctrl+Enter combination
        if (e is not { Key: Key.Enter, KeyModifiers: KeyModifiers.Control } ||
            DataContext is not FloatingActionMenuViewModel vm || !vm.GoToNextStepCommand.CanExecute(null)) return;
        
        // Execute the command to proceed to the next step
        vm.GoToNextStepCommand.Execute(null);
            
        // Mark the event as handled to prevent the TextBox from adding a newline
        e.Handled = true;
    }
    
    private void MultilineTextBox_OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            // Subscribe to the KeyDown event during the tunneling phase.
            textBox.AddHandler(KeyDownEvent, MultilineTextBox_OnKeyDownHandler, RoutingStrategies.Tunnel, handledEventsToo: true);
        }
    }

    private void MultilineTextBox_OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            // Clean up the event handler to prevent memory leaks.
            textBox.RemoveHandler(KeyDownEvent, MultilineTextBox_OnKeyDownHandler);
        }
    }
    
    private void ListOptions_OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        _currentListOptionsControl = sender as ItemsControl;
    }

    private async void BrowseFileButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not FloatingActionMenuViewModel { CurrentStepViewModel: FilePickerStepViewModel filePickerVm }) return;
        
        _isPreventClose = true;
        
        var dialogService = Ioc.Default.GetRequiredService<IDialogService>();
        var filePath = await dialogService.ShowOpenFileDialogAsync("Select File", "All Files", Constants.SupportedDocumentExtensions.Select(ex => $"*{ex}").ToArray());
        if (!string.IsNullOrWhiteSpace(filePath)) await filePickerVm.ValidateAndSetFileAsync(filePath);

        _isPreventClose = false;
    }

    #endregion

    #region ViewModel Interaction

    private void OnRequestClose()
    {
        if (_isPreventClose) return;
        Close();
    }
    
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not FloatingActionMenuViewModel vm) return;

        switch (e.PropertyName)
        {
            case nameof(FloatingActionMenuViewModel.SelectedItem):
                ScrollSelectedItemIntoView();
                break;

            case nameof(FloatingActionMenuViewModel.CurrentStepViewModel):
                if (_currentListStepViewModel != null)
                    _currentListStepViewModel.PropertyChanged -= OnStepViewModelPropertyChanged;
                
                // Check if the new step is a list-based step (Choice or Boolean)
                if (vm.CurrentStepViewModel is ChoiceStepViewModel newChoiceVm)
                {
                    _currentListStepViewModel = newChoiceVm;
                    _currentListStepViewModel.PropertyChanged += OnStepViewModelPropertyChanged;
                    FocusListItemButton(newChoiceVm.SelectedOption);
                }
                else if (vm.CurrentStepViewModel is BooleanStepViewModel newBoolVm)
                {
                    _currentListStepViewModel = newBoolVm;
                    _currentListStepViewModel.PropertyChanged += OnStepViewModelPropertyChanged;
                    FocusListItemButton(newBoolVm.SelectedOption);
                }
                else
                {
                    _currentListStepViewModel = null;
                }
                break;
        }
    }
    
    private void OnStepViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // When the selected option *within* a list-based step changes, update the UI focus.
        if (e.PropertyName != nameof(ChoiceStepViewModel.SelectedOption)) return;
        
        object? selectedOption = null;
        if (sender is ChoiceStepViewModel choiceVm)
        {
            selectedOption = choiceVm.SelectedOption;
        }
        else if (sender is BooleanStepViewModel boolVm)
        {
            selectedOption = boolVm.SelectedOption;
        }
        
        FocusListItemButton(selectedOption);
    }

    #endregion

    #region UI Manipulation Logic

    /// <summary>
    /// Finds the UI element corresponding to the currently selected item in the ViewModel
    /// and ensures it is visible within the ScrollViewer.
    /// </summary>
    private void ScrollSelectedItemIntoView()
    {
        if (DataContext is not FloatingActionMenuViewModel { SelectedItem: { } selectedItem }) return;
        
        // Post to the dispatcher to ensure the UI has updated after the data context change.
        Dispatcher.UIThread.Post(() =>
        {
            var container = this.GetVisualDescendants()
                .OfType<Control>()
                .FirstOrDefault(c => c.DataContext == selectedItem);

            container?.BringIntoView();
        }, DispatcherPriority.Background);
    }
    
    /// <summary>
    /// A generic method to focus the button corresponding to a selected option in a list-based step.
    /// </summary>
    /// <param name="optionVm">The ViewModel of the selected option (e.g., ChoiceOptionViewModel or BooleanOptionViewModel).</param>
    private void FocusListItemButton(object? optionVm)
    {
        if (_currentListOptionsControl is null || optionVm is null) return;
        
        // Post to dispatcher to run after the UI elements for the choices have been created.
        Dispatcher.UIThread.Post(() =>
        {
            var itemContainer = _currentListOptionsControl.ItemsPanelRoot?
                .Children
                .OfType<ContentPresenter>()
                .FirstOrDefault(c => c.DataContext == optionVm);

            var button = itemContainer?.GetVisualChildren().FirstOrDefault() as Control;
            button?.Focus(NavigationMethod.Directional);
        }, DispatcherPriority.Background);
    }
    
    #endregion
    
    
}