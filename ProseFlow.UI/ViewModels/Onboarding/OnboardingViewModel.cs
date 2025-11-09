using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using ProseFlow.Application.DTOs;
using ProseFlow.Application.Events;
using ProseFlow.Application.Interfaces;
using ProseFlow.Application.Services;
using ProseFlow.Core.Interfaces.Os;
using ProseFlow.Core.Models;
using ProseFlow.UI.ViewModels.Providers;
using Action = System.Action;

namespace ProseFlow.UI.ViewModels.Onboarding;

public enum OnboardingStep
{
    Welcome,
    ProviderChoice,
    CloudSetup,
    LocalSetup,
    ActionsIntro,
    TemplateTutorial,
    HotkeySetup,
    HotkeyTutorial,
    WorkspaceIntro,
    Graduation
}

public partial class PresetOnboardingViewModel(PresetDto preset) : ViewModelBase
{
    [ObservableProperty]
    private bool _isSelected = true; // Default to selected
    public PresetDto Preset { get; } = preset;
}

/// <summary>
/// A ViewModel for a single step in the visual progress indicator.
/// </summary>
public partial class OnboardingProgressStep : ObservableObject
{
    public required string Title { get; init; }
    [ObservableProperty] private bool _isCompleted;
    [ObservableProperty] private bool _isCurrent;
}


public partial class OnboardingViewModel(
    IServiceProvider serviceProvider,
    SettingsService settingsService,
    CloudProviderManagementService cloudProviderService,
    IPresetService presetService,
    IHotkeyService hotkeyService,
    ISystemService systemService) : ViewModelBase
{
    public event Action? RequestClose;
    public bool IsCompletedSuccessfully { get; private set; }

    [ObservableProperty]
    private OnboardingStep _currentStep = OnboardingStep.Welcome;

    [ObservableProperty]
    private ViewModelBase? _currentContentViewModel;

    // Data collected during onboarding
    public CloudProviderConfiguration? CloudProviderConfig { get; set; }
    public string? LocalModelPath { get; set; }
    public bool LaunchAtLogin { get; set; } = true;
    [ObservableProperty]
    private string _actionMenuHotkey = "Ctrl+J";

    // Button Visibility and Enabled States
    [ObservableProperty]
    private bool _isBackButtonVisible;

    [ObservableProperty]
    private bool _isNextButtonEnabled = true;

    [ObservableProperty]
    private string _nextButtonText = "Continue";
    
    public ObservableCollection<PresetOnboardingViewModel> AvailablePresets { get; } = [];
    public ObservableCollection<OnboardingProgressStep> ProgressSteps { get; } = [];

    public OnboardingViewModel() : this(
        Ioc.Default.GetRequiredService<IServiceProvider>(), 
        Ioc.Default.GetRequiredService<SettingsService>(), 
        Ioc.Default.GetRequiredService<CloudProviderManagementService>(),
        Ioc.Default.GetRequiredService<IPresetService>(),
        Ioc.Default.GetRequiredService<IHotkeyService>(),
        Ioc.Default.GetRequiredService<ISystemService>()) {}

    public async Task InitializeAsync()
    {
        InitializeProgressSteps();
        UpdateProgressIndicator(CurrentStep);
        
        var presets = await presetService.GetAvailablePresetsAsync();
        foreach (var preset in presets)
        {
            AvailablePresets.Add(new PresetOnboardingViewModel(preset));
        }
    }
    
    private void InitializeProgressSteps()
    {
        ProgressSteps.Add(new OnboardingProgressStep { Title = "Welcome" });
        ProgressSteps.Add(new OnboardingProgressStep { Title = "Setup" });
        ProgressSteps.Add(new OnboardingProgressStep { Title = "Actions" });
        ProgressSteps.Add(new OnboardingProgressStep { Title = "Hotkeys" });
        ProgressSteps.Add(new OnboardingProgressStep { Title = "Finish" });
    }

    private void UpdateProgressIndicator(OnboardingStep newStep)
    {
        var stepIndex = newStep switch
        {
            OnboardingStep.Welcome => 0,
            OnboardingStep.ProviderChoice or OnboardingStep.CloudSetup or OnboardingStep.LocalSetup => 1,
            OnboardingStep.ActionsIntro or OnboardingStep.TemplateTutorial => 2,
            OnboardingStep.HotkeySetup or OnboardingStep.HotkeyTutorial => 3,
            _ => 4,
        };

        for (int i = 0; i < ProgressSteps.Count; i++)
        {
            ProgressSteps[i].IsCurrent = (i == stepIndex);
            ProgressSteps[i].IsCompleted = (i < stepIndex);
        }
    }

    partial void OnCurrentStepChanged(OnboardingStep value)
    {
        UpdateStep(value);
    }

    private void UpdateStep(OnboardingStep newStep)
    {
        UpdateProgressIndicator(newStep);
        IsBackButtonVisible = newStep > OnboardingStep.Welcome;
        NextButtonText = newStep switch
        {
            OnboardingStep.Graduation => "Finish",
            OnboardingStep.WorkspaceIntro => "I'll Explore This Later",
            _ => "Continue"
        };

        // Unsubscribe from previous VM events and global events
        if (CurrentContentViewModel is IDisposable disposable) disposable.Dispose();
        if (CurrentContentViewModel is CloudOnboardingViewModel oldCloudVm) oldCloudVm.PropertyChanged -= OnContentViewModelPropertyChanged;
        if (CurrentContentViewModel is ModelLibraryViewModel oldLocalVm) oldLocalVm.PropertyChanged -= OnContentViewModelPropertyChanged;
        if (CurrentContentViewModel is HotkeyTutorialViewModel oldTutorialVm)
        {
            oldTutorialVm.PropertyChanged -= OnContentViewModelPropertyChanged;
            hotkeyService.ActionMenuHotkeyPressed -= OnTutorialHotkeyPressed; // Unsubscribe
        }

        // Set the new content view model and enable/disable next button
        switch (newStep)
        {
            case OnboardingStep.CloudSetup:
                var cloudVm = serviceProvider.GetRequiredService<CloudOnboardingViewModel>();
                cloudVm.PropertyChanged += OnContentViewModelPropertyChanged;
                CurrentContentViewModel = cloudVm;
                IsNextButtonEnabled = cloudVm.Status == TestStatus.Success;
                break;
            case OnboardingStep.LocalSetup:
                var localVm = serviceProvider.GetRequiredService<ModelLibraryViewModel>();
                localVm.IsOnboardingMode = true;
                localVm.PropertyChanged += OnContentViewModelPropertyChanged;
                _ = localVm.OnNavigatedToAsync();
                CurrentContentViewModel = localVm;
                IsNextButtonEnabled = localVm.IsAModelSelected;
                break;
            case OnboardingStep.TemplateTutorial:
                CurrentContentViewModel = serviceProvider.GetRequiredService<TemplateTutorialViewModel>();
                IsNextButtonEnabled = true;
                break;
            case OnboardingStep.HotkeyTutorial:
                var tutorialVm = serviceProvider.GetRequiredService<HotkeyTutorialViewModel>();
                tutorialVm.PropertyChanged += OnContentViewModelPropertyChanged;
                tutorialVm.ConfiguredHotkey = ActionMenuHotkey; // Pass the configured hotkey
                CurrentContentViewModel = tutorialVm;
                IsNextButtonEnabled = tutorialVm.IsCompleted;
                hotkeyService.ActionMenuHotkeyPressed += OnTutorialHotkeyPressed; // Subscribe
                break;
            default:
                CurrentContentViewModel = null; // For simple steps handled by DataTemplates
                IsNextButtonEnabled = true;
                break;
        }
    }

    private void OnTutorialHotkeyPressed()
    {
        // Ensure this only triggers the UI on the tutorial step.
        if (CurrentStep != OnboardingStep.HotkeyTutorial || CurrentContentViewModel is not HotkeyTutorialViewModel vm) return;
        
        // The event might come from a background thread, so dispatch to UI thread.
        Avalonia.Threading.Dispatcher.UIThread.Post(() => vm.ShowMenuCommand.Execute(null));
    }

    private void OnContentViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Dynamically enable the 'Next' button based on sub-viewmodel state
        IsNextButtonEnabled = sender switch
        {
            CloudOnboardingViewModel cloudVm when e.PropertyName == nameof(CloudOnboardingViewModel.Status) => cloudVm.Status == TestStatus.Success,
            ModelLibraryViewModel localVm when e.PropertyName == nameof(ModelLibraryViewModel.IsAModelSelected) => localVm.IsAModelSelected,
            HotkeyTutorialViewModel tutorialVm when e.PropertyName == nameof(HotkeyTutorialViewModel.IsCompleted) => tutorialVm.IsCompleted,
            _ => IsNextButtonEnabled
        };
    }

    [RelayCommand]
    private void NextStep()
    {
        switch (CurrentStep)
        {
            // Final step, close the dialog with a success result
            case OnboardingStep.Graduation:
                IsCompletedSuccessfully = true;
                RequestClose?.Invoke();
                return;
            // Handle branching from provider choice
            case OnboardingStep.ProviderChoice:
                // This is handled by choice-specific buttons in the view.
                return;
            // Before leaving setup steps, capture the data
            case OnboardingStep.CloudSetup when CurrentContentViewModel is CloudOnboardingViewModel cloudVm:
                CloudProviderConfig = cloudVm.GetConfiguration();
                break;
            case OnboardingStep.LocalSetup when CurrentContentViewModel is ModelLibraryViewModel localVm:
                LocalModelPath = localVm.SelectedModel?.Model.FilePath;
                break;
            case OnboardingStep.HotkeySetup:
                hotkeyService.UpdateHotkeys(ActionMenuHotkey, "Ctrl+Shift+V");
                break;
        }

        // Go to the next logical step
        var nextStep = CurrentStep switch
        {
            OnboardingStep.CloudSetup or OnboardingStep.LocalSetup => OnboardingStep.ActionsIntro,
            OnboardingStep.ActionsIntro => OnboardingStep.TemplateTutorial,
            OnboardingStep.TemplateTutorial => OnboardingStep.HotkeySetup,
            OnboardingStep.HotkeyTutorial => OnboardingStep.WorkspaceIntro,
            OnboardingStep.WorkspaceIntro => OnboardingStep.Graduation,
            _ => CurrentStep + 1
        };

        CurrentStep = nextStep;
    }
    
    [RelayCommand]
    private void SkipOnboarding()
    {
        IsCompletedSuccessfully = true;
        RequestClose?.Invoke();
    }

    [RelayCommand]
    private void ChooseProviderPath(string path)
    {
        CurrentStep = path == "Cloud" ? OnboardingStep.CloudSetup : OnboardingStep.LocalSetup;
    }

    [RelayCommand]
    private void PreviousStep()
    {
        if (CurrentStep == OnboardingStep.Welcome) return;

        // If coming back from a setup path, go to provider choice
        var prevStep = CurrentStep switch
        {
            OnboardingStep.ActionsIntro => OnboardingStep.ProviderChoice,
            OnboardingStep.CloudSetup => OnboardingStep.ProviderChoice,
            OnboardingStep.LocalSetup => OnboardingStep.ProviderChoice,
            OnboardingStep.TemplateTutorial => OnboardingStep.ActionsIntro,
            OnboardingStep.HotkeySetup => OnboardingStep.TemplateTutorial,
            OnboardingStep.WorkspaceIntro => OnboardingStep.HotkeyTutorial,
            OnboardingStep.Graduation => OnboardingStep.WorkspaceIntro,
            _ => CurrentStep - 1
        };

        CurrentStep = prevStep;
    }

    public async Task SaveSettingsAsync()
    {
        var generalSettings = await settingsService.GetGeneralSettingsAsync();
        var providerSettings = await settingsService.GetProviderSettingsAsync();

        generalSettings.LaunchAtLogin = LaunchAtLogin;
        generalSettings.ActionMenuHotkey = ActionMenuHotkey;
        systemService.SetLaunchAtLogin(LaunchAtLogin);

        if (CloudProviderConfig is not null)
        {
            providerSettings.PrimaryServiceType = "Cloud";
            await cloudProviderService.CreateConfigurationAsync(CloudProviderConfig);
        }
        else if (!string.IsNullOrWhiteSpace(LocalModelPath))
        {
            providerSettings.PrimaryServiceType = "Local";
            providerSettings.LocalModelPath = LocalModelPath;
        }

        // Import selected presets
        var presetsToImport = AvailablePresets.Where(p => p.IsSelected).ToList();
        if (presetsToImport.Count > 0)
        {
            foreach (var presetVm in presetsToImport)
            {
                await presetService.ImportPresetAsync(presetVm.Preset.ResourcePath);
            }
            AppEvents.RequestNotification($"{presetsToImport.Count} preset group(s) imported!", NotificationType.Success);
        }

        await settingsService.SaveGeneralSettingsAsync(generalSettings);
        await settingsService.SaveProviderSettingsAsync(providerSettings);
    }
    
    public void OnClosing()
    {
        hotkeyService.ActionMenuHotkeyPressed -= OnTutorialHotkeyPressed;
    }
}