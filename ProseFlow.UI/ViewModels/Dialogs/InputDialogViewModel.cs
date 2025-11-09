using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ProseFlow.UI.Models;

namespace ProseFlow.UI.ViewModels.Dialogs;

/// <summary>
/// A reusable ViewModel for a dialog that captures a single string input from the user.
/// </summary>
public partial class InputDialogViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _title = "Input Required";

    [ObservableProperty]
    private string _message = "Please provide a value:";

    [ObservableProperty]
    private string _confirmButtonText = "Confirm";

    [ObservableProperty]
    private string _inputText = string.Empty;
    
    public readonly TaskCompletionSource<InputDialogResult> CompletionSource = new();

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
        CompletionSource.TrySetResult(new InputDialogResult(true, InputText));
    }

    [RelayCommand]
    private void Cancel()
    {
        CompletionSource.TrySetResult(new InputDialogResult(false, null));
    }
}