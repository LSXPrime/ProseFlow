using System;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using ProseFlow.Core.Models;
using ProseFlow.UI.ViewModels.Actions;
using ShadUI;
using Window = ShadUI.Window;

namespace ProseFlow.UI.Views.Actions;

public partial class ActionEditorView : Window
{
    private TextBox? _instructionTextBox;
    
    public ActionEditorView()
    {
        InitializeComponent();
    }
    
    private async void Window_OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is ActionEditorViewModel vm) await vm.OnNavigatedToAsync();
    }
    
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _instructionTextBox = this.FindControl<TextBox>("InstructionTextBox");
    }

    private void InsertPlaceholderButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: ActionPlaceholderViewModel placeholderVm } || 
            DataContext is not ActionEditorViewModel editorVm || 
            _instructionTextBox is null)
        {
            return;
        }
        
        var caretIndex = _instructionTextBox.CaretIndex;
        editorVm.InsertPlaceholderText(placeholderVm.Placeholder.Name, caretIndex);
        
        // After inserting, focus back on the textbox and move the caret to after the inserted text
        _instructionTextBox.Focus();
        _instructionTextBox.CaretIndex = caretIndex + $"[{placeholderVm.Placeholder.Name}]".Length;
    }

    private void SmartTokenButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: SmartTokenInfo tokenInfo } button) return;

        // Find the dropdown that contains this button to get the TextBox reference from its Tag.
        var dropdown = button.Parent?.Parent?.Parent as SimpleDropdown;
        if (dropdown?.Tag is not TextBox textBox) return;

        // Insert the token at the current caret position.
        var caretIndex = textBox.CaretIndex;
        textBox.Text = (textBox.Text ?? string.Empty).Insert(caretIndex, tokenInfo.Token);
        textBox.CaretIndex = caretIndex + tokenInfo.Token.Length;
        textBox.Focus();

        // Close the dropdown after selection.
        dropdown.IsDropDownOpen = false;
        e.Handled = true;
    }

    private void Close_OnPointerPressed(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private async void Save_OnPointerPressed(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ActionEditorViewModel vm) await vm.SaveAsync();
        Close();
    }
}