using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ProseFlow.UI.ViewModels.Onboarding;

public partial class HotkeyTutorialViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _headerText = "Let's try it!";
    
    [ObservableProperty]
    private string _instructionText = "Select the text below and press the hotkey to see how ProseFlow helps you review changes.";
    
    [ObservableProperty]
    private string _configuredHotkey = "Ctrl+J";

    [ObservableProperty]
    private string _sampleText = "ProseFlow is a grate tool it hlps me writng better";

    [ObservableProperty]
    private bool _showSimulatedDiffView;

    [ObservableProperty]
    private bool _isCompleted;

    [RelayCommand]
    private void ShowMenu()
    {
        ShowSimulatedDiffView = true;
    }

    [RelayCommand]
    private void SimulateAcceptDiff()
    {
        ShowSimulatedDiffView = false;
        SampleText = "✨ ProseFlow is a great tool. It helps me write better. ✨";
        HeaderText = "NICE! That's the magic.";
        InstructionText = "You can review, refine, or regenerate any result. You can also change the default output mode in Settings.";
        IsCompleted = true;
    }
}