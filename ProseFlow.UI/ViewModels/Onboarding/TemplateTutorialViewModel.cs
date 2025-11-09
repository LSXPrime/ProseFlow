using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ProseFlow.UI.ViewModels.Onboarding;

/// <summary>
/// A ViewModel to manage the state of the interactive template simulation in the onboarding process.
/// </summary>
public partial class TemplateTutorialViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _recipient = string.Empty;

    [ObservableProperty]
    private string _topic = string.Empty;

    [ObservableProperty]
    private string? _selectedTone;

    [ObservableProperty]
    private string _simulatedOutput = string.Empty;

    [ObservableProperty]
    private bool _isOutputVisible;

    public ObservableCollection<string> AvailableTones { get; } = ["Formal", "Casual", "Urgent"];

    public TemplateTutorialViewModel()
    {
        _selectedTone = AvailableTones[0];
    }

    [RelayCommand]
    private void SimulateRun()
    {
        // Construct the simulated output based on user input.
        SimulatedOutput = $"Subject: Regarding {Topic}\n\n" +
                          $"Dear {Recipient},\n\n" +
                          $"I am writing to you today to discuss the {Topic}. " +
                          $"This is a sample of how a templated action can dynamically generate content. " +
                          $"The tone for this communication has been set to '{SelectedTone}'.\n\n" +
                          $"Best regards,\nProseFlow";

        IsOutputVisible = true;
    }
}