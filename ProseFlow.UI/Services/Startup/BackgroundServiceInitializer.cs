using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ProseFlow.Application.Interfaces;
using ProseFlow.Application.Services;
using ProseFlow.Core.Interfaces.Os;
using ProseFlow.Infrastructure.Services.AiProviders.Local;
using ProseFlow.UI.ViewModels.Windows;

namespace ProseFlow.UI.Services.Startup;

/// <summary>
/// Initializes and starts background services required for the application to function.
/// </summary>
public static class BackgroundServiceInitializer
{
    /// <summary>
    /// Starts all long-running and background services.
    /// </summary>
    /// <param name="services">The configured service provider.</param>
    /// <param name="splashViewModel">The splash screen view model for reporting progress.</param>
    public static async Task StartServicesAsync(IServiceProvider services, SplashScreenViewModel splashViewModel)
    {
        var logger = services.GetRequiredService<ILogger<App>>();
        splashViewModel.Report("Loading services...");

        // Initialize local model native manager
        var nativeManager = services.GetRequiredService<LocalNativeManager>();
        nativeManager.Initialize();

        // Initialize services that depend on the database
        var usageTrackingService = services.GetRequiredService<UsageTrackingService>();
        await usageTrackingService.InitializeAsync();
        var settingsService = services.GetRequiredService<SettingsService>();
        var workspaceManager = services.GetRequiredService<IWorkspaceManager>();
        await workspaceManager.LoadStateAsync();

        // Perform silent update check on startup
        var updateService = services.GetRequiredService<IUpdateService>();
        _ = Task.Run(async () =>
        {
            await Task.Delay(5000); // Wait 5 seconds to not impact startup time
            await updateService.CheckForUpdateAsync();
        });

        // Check for local model on startup
        await using (var scope = services.CreateAsyncScope())
        {
            var modelManager = scope.ServiceProvider.GetRequiredService<LocalModelManagerService>();
            try
            {
                var providerSettings = await settingsService.GetProviderSettingsAsync();
                if (providerSettings is { PrimaryServiceType: "Local", LocalModelLoadOnStartup: true })
                {
                    if (string.IsNullOrWhiteSpace(providerSettings.LocalModelPath) ||
                        !File.Exists(providerSettings.LocalModelPath))
                    {
                        logger.LogWarning("Auto-load skipped: Local model path is not configured or file does not exist.");
                    }
                    else
                    {
                        splashViewModel.Report("Loading local model...");
                        logger.LogInformation("Attempting to auto-load local model on startup...");
                        // Don't auto-load model in design mode
                        if (!Avalonia.Controls.Design.IsDesignMode) 
                            _ = modelManager.LoadModelAsync(providerSettings);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred during the local model auto-load check.");
            }
        }
        
        // Initialize and start background services
        var orchestrationService = services.GetRequiredService<ActionOrchestrationService>();
        orchestrationService.Initialize();
        
        // Initialize the floating button service
        var floatingOrbService = services.GetRequiredService<FloatingOrbService>();
        floatingOrbService.Initialize();
        
        // Hook up hotkeys
        var hotkeyService = services.GetRequiredService<IHotkeyService>();
        var generalSettings = await settingsService.GetGeneralSettingsAsync();
        _ = hotkeyService.StartHookAsync();
        hotkeyService.UpdateHotkeys(generalSettings.ActionMenuHotkey, generalSettings.SmartPasteHotkey);
        
        // Set the initial state of the floating button based on settings
        floatingOrbService.SetEnabled(!generalSettings.IsFloatingButtonHidden);
    }
}