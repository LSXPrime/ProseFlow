using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using ProseFlow.Application.Events;
using ProseFlow.Application.Services;
using ProseFlow.UI.ViewModels.Onboarding;
using ProseFlow.UI.Views.Onboarding;
using ProseFlow.UI.Views.Windows;

namespace ProseFlow.UI.Services.Startup;

/// <summary>
/// Orchestrates the final steps of the application startup, determining whether to show the onboarding flow or the main window.
/// </summary>
public static class PostStartupOrchestrator
{
    /// <summary>
    /// Executes the final startup logic, such as showing the onboarding window or the main application window.
    /// </summary>
    public static async Task RunAsync(IClassicDesktopStyleApplicationLifetime desktop, IServiceProvider services)
    {
        if (desktop.MainWindow is null) 
            return; // This should never happen, but just to handle the nullability 
        
        var settingsService = services.GetRequiredService<SettingsService>();
        var generalSettings = await settingsService.GetGeneralSettingsAsync();
        
        // Onboarding is the last step. It runs on top of the fully initialized but hidden application.
        if (!generalSettings.IsOnboardingCompleted)
        {
            // Disable the floating menu while onboarding is active.
            AppEvents.IsShowFloatingMenuEnabled = false;

            var onboardingVm = services.GetRequiredService<OnboardingViewModel>();
            var onboardingWindow = new OnboardingWindow { DataContext = onboardingVm };

            // Handle the closing of the non-modal onboarding window to determine the next step.
            onboardingWindow.Closing += async (_, _) =>
            {
                // Re-enable the floating menu regardless of outcome.
                AppEvents.IsShowFloatingMenuEnabled = true;

                if (onboardingVm.IsCompletedSuccessfully)
                {
                    await onboardingVm.SaveSettingsAsync();
                    
                    var freshSettings = await settingsService.GetGeneralSettingsAsync();
                    freshSettings.IsOnboardingCompleted = true;
                    await settingsService.SaveGeneralSettingsAsync(freshSettings);
                    
                    desktop.MainWindow.Show();
                    desktop.MainWindow.Activate();
                    AppEvents.OnMainWindowVisibilityChanged(true);
                }
                else
                {
                    Dispatcher.UIThread.Post(() => desktop.Shutdown());
                }
            };
            
            // Hide splash screen, in case it was still open due to a critical error.
            if (desktop.Windows.Count > 0 && desktop.Windows.FirstOrDefault(x => x is SplashScreenWindow) is SplashScreenWindow splash)
                splash.Close();
            
            desktop.MainWindow.Show();
            onboardingWindow.Show(desktop.MainWindow);
        }
        else
        {
            // For returning users, show the main window or start minimized.
            if (generalSettings.StartMinimized)
            {
                // App starts hidden, only tray icon is visible.
                desktop.MainWindow.Hide();
                desktop.MainWindow.WindowState = WindowState.Minimized;
                AppEvents.OnMainWindowVisibilityChanged(false);
            }
            else
            {
                // Normal startup, show the main window.
                desktop.MainWindow.Show();
                AppEvents.OnMainWindowVisibilityChanged(true);
            }
        }
    }
}