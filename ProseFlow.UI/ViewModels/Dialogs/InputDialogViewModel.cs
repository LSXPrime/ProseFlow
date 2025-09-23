﻿using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShadUI;

namespace ProseFlow.UI.ViewModels.Dialogs;

/// <summary>
/// A reusable ViewModel for a dialog that captures a single string input from the user.
/// </summary>
public partial class InputDialogViewModel(DialogManager dialogManager) : ViewModelBase
{
    [ObservableProperty]
    private string _title = "Input Required";

    [ObservableProperty]
    private string _message = "Please provide a value:";

    [ObservableProperty]
    private string _confirmButtonText = "Confirm";

    [ObservableProperty]
    private string _inputText = string.Empty;

    /// <summary>
    /// Initializes the ViewModel with the necessary display text and an optional initial value.
    /// </summary>
    public void Initialize(string title, string message, string confirmButtonText, string? initialValue = null)
    {
        Title = title;
        Message = message;
        ConfirmButtonText = confirmButtonText;
        InputText = initialValue ?? string.Empty;
    }

    [RelayCommand]
    private void Submit()
    {
        // Close the dialog, signaling success. The DialogService will retrieve the InputText.
        dialogManager.Close(this, new CloseDialogOptions { Success = true });
    }

    [RelayCommand]
    private void Cancel()
    {
        // Close the dialog, signaling cancellation.
        dialogManager.Close(this);
    }
}