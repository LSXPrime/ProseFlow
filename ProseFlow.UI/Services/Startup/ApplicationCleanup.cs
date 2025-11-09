using System;
using Avalonia.Controls.ApplicationLifetimes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ProseFlow.Core.Interfaces.Os;
using ProseFlow.Infrastructure.Services.AiProviders.Local;
using ProseFlow.Infrastructure.Services.Monitoring;

namespace ProseFlow.UI.Services.Startup;

/// <summary>
/// Handles the cleanup of resources when the application exits.
/// </summary>
public static class ApplicationCleanup
{
    /// <summary>
    /// Disposes of services and performs necessary cleanup actions upon application exit.
    /// </summary>
    public static void OnExit(object? sender, ControlledApplicationLifetimeExitEventArgs e, IServiceProvider services)
    {
        var logger = services.GetService<ILogger<App>>();
        logger?.LogInformation("Application exit requested. Cleaning up resources...");

        // Dispose the MainViewModel, which will cascade disposals down the ViewModel tree
        if (sender is IClassicDesktopStyleApplicationLifetime { MainWindow.DataContext: IDisposable disposable })
            disposable.Dispose();

        // Dispose singleton infrastructure services
        services.GetService<HardwareMonitoringService>()?.Dispose();
        services.GetService<IHotkeyService>()?.Dispose();
        services.GetService<LocalModelManagerService>()?.UnloadModel();

        logger?.LogInformation("Cleanup complete. Application will now exit.");
    }
}