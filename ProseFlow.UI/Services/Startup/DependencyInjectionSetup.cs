using System;
using System.IO;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ProseFlow.Application.Interfaces;
using ProseFlow.Application.Services;
using ProseFlow.Core.Interfaces;
using ProseFlow.Core.Interfaces.Os;
using ProseFlow.Core.Models;
using ProseFlow.Infrastructure.Data;
using ProseFlow.Infrastructure.Security;
using ProseFlow.Infrastructure.Services.AiProviders;
using ProseFlow.Infrastructure.Services.AiProviders.Local;
using ProseFlow.Infrastructure.Services.Documents;
using ProseFlow.Infrastructure.Services.Models;
using ProseFlow.Infrastructure.Services.Monitoring;
using ProseFlow.Infrastructure.Services.Os;
using ProseFlow.Infrastructure.Services.Os.Clipboard;
using ProseFlow.Infrastructure.Services.Os.Hotkeys;
using ProseFlow.Infrastructure.Services.Updates;
using ProseFlow.UI.Services.ActiveWindow;
using ProseFlow.UI.Services.Logging;
using ProseFlow.UI.ViewModels;
using ProseFlow.UI.ViewModels.About;
using ProseFlow.UI.ViewModels.Actions;
using ProseFlow.UI.ViewModels.Dashboard;
using ProseFlow.UI.ViewModels.Dialogs;
using ProseFlow.UI.ViewModels.Downloads;
using ProseFlow.UI.ViewModels.History;
using ProseFlow.UI.ViewModels.Onboarding;
using ProseFlow.UI.ViewModels.Providers;
using ProseFlow.UI.ViewModels.Settings;
using ProseFlow.UI.ViewModels.Windows;
using ProseFlow.UI.Views.Windows;
using Serilog;
using Serilog.Events;
using ShadUI;

namespace ProseFlow.UI.Services.Startup;

/// <summary>
/// Configures the dependency injection container for the application.
/// </summary>
public static class DependencyInjectionSetup
{
    /// <summary>
    /// Creates and configures the service collection, then builds the service provider.
    /// </summary>
    /// <returns>A configured <see cref="IServiceProvider"/>.</returns>
    public static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection()
            .AddLoggingAndDataProtection()
            .AddDatabaseAndSecurity()
            .AddInfrastructureServices()
            .AddApplicationServices()
            .AddPlatformSpecificServices()
            .AddUiServicesAndViewModels();

        return services.BuildServiceProvider();
    }

    private static IServiceCollection AddLoggingAndDataProtection(this IServiceCollection services)
    {
        var proseFlowDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ProseFlow");
        Directory.CreateDirectory(proseFlowDataPath);
        Directory.CreateDirectory(Constants.LogDirectoryPath);

        // The template to use for formatting log messages.
        const string outputTemplate = "[{Timestamp:HH:mm:ss} {Level:u3}] [{ClassName}] {Message:lj}{NewLine}{Exception}";

        // Create and register the log collector instance so Serilog can use it.
        var logCollector = new ApplicationLogCollectorService(outputTemplate);
        services.AddSingleton(logCollector);

        var logPath = Path.Combine(Constants.LogDirectoryPath, "proseflow-.log");

        // Create the logger instance.
        var logger = new LoggerConfiguration()
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", LogEventLevel.Warning)
            .MinimumLevel.Override("Velopack", LogEventLevel.Information)
            .Enrich.FromLogContext()
            .Enrich.With<ClassNameEnricher>()
#if DEBUG
            .WriteTo.Debug()
            .WriteTo.Console(outputTemplate: outputTemplate)
#endif
            .WriteTo.File(logPath,
                rollingInterval: RollingInterval.Day,
                outputTemplate: outputTemplate)
            .WriteTo.Sink(logCollector)
            .CreateLogger();

        Log.Logger = logger;

        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddSerilog(logger, dispose: true);
        });

        services.AddDataProtection()
            .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(proseFlowDataPath, "keys")))
            .SetApplicationName("ProseFlow");
        
        return services;
    }
    
    private static IServiceCollection AddDatabaseAndSecurity(this IServiceCollection services)
    {
        var proseFlowDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ProseFlow");
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlite($"Data Source={Path.Combine(proseFlowDataPath, "proseflow.db")}"));

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddSingleton<ApiKeyProtector>();
        
        return services;
    }

    private static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
    {
        services.AddSingleton<UsageTrackingService>();
        services.AddSingleton<LocalModelManagerService>();
        services.AddSingleton<ILocalSessionService, LocalSessionService>();
        services.AddSingleton<IAiProvider, CloudProvider>();
        services.AddSingleton<IAiProvider, LocalProvider>();
        services.AddSingleton<HardwareMonitoringService>();
        services.AddSingleton<LocalNativeManager>();
        
        // Add System Services
        services.AddSingleton<HotkeyRecordingService>();
        services.AddSingleton<IHotkeyRecordingService>(sp => sp.GetRequiredService<HotkeyRecordingService>());
        services.AddSingleton<IHotkeyService, HotkeyService>();
        services.AddSingleton<ISystemService, SystemService>();
        
        // Add Clipboard Services
        services.AddKeyedSingleton<IFallbackClipboardService, NativeShellClipboardService>("NativeShellClipboardService");
        services.AddKeyedSingleton<IFallbackClipboardService, AvaloniaClipboardService>("AvaloniaClipboardService");
        services.AddKeyedSingleton<IFallbackClipboardService, TextCopyClipboardService>("TextCopyClipboardService");
        services.AddSingleton<IClipboardService, ClipboardService>();
        
        // Model Download Services
        services.AddSingleton<IModelCatalogService, ModelCatalogService>();
        services.AddSingleton<IDownloadManager, DownloadManager>();
        services.AddSingleton<ILocalModelManagementService, LocalModelManagementService>();

        // Document Reader Service
        services.AddScoped<IDocumentReaderService, DocumentReaderService>();

        // Update Service
        services.AddSingleton<IUpdateService, UpdateService>();

        return services;
    }

    private static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddSingleton<IBackgroundActionTrackerService, BackgroundActionTrackerService>();
        services.AddSingleton<ActionOrchestrationService>();
        services.AddScoped<DashboardService>();
        services.AddScoped<ActionManagementService>();
        services.AddScoped<SettingsService>();
        services.AddScoped<HistoryService>();
        services.AddScoped<CloudProviderManagementService>();
        services.AddSingleton<IPresetService, PresetService>();
        
        // Workspace Services
        services.AddSingleton<IWorkspaceWatcherService, WorkspaceWatcherService>();
        services.AddSingleton<IWorkspaceProtector, WorkspaceProtector>();
        services.AddSingleton<IWorkspaceManager, WorkspaceManager>();
        services.AddScoped<WorkspaceSyncService>();

        // Template Services
        services.AddTransient<TemplateEngineService>();

        return services;
    }

    private static IServiceCollection AddPlatformSpecificServices(this IServiceCollection services)
    {
        if (OperatingSystem.IsLinux())
            services.AddSingleton<IActiveWindowService, LinuxActiveWindowTracker>();
        else if (OperatingSystem.IsWindows())
            services.AddSingleton<IActiveWindowService, WindowsActiveWindowTracker>();
        else if (OperatingSystem.IsMacOS())
            services.AddSingleton<IActiveWindowService, MacOsActiveWindowTracker>();
        else
            services.AddSingleton<IActiveWindowService, DefaultActiveWindowTracker>();
        
        return services;
    }

    private static IServiceCollection AddUiServicesAndViewModels(this IServiceCollection services)
    {
        // UI Services
        services.AddSingleton<DialogManager>();
        services.AddSingleton<ToastManager>();
        services.AddSingleton<NotificationService>();
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<FloatingOrbService>();
        
        // Singleton ViewModels
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<TrayIconViewModel>();
        
        // Transient Page and Feature ViewModels
        services.AddTransient<FloatingOrbMenuViewModel>();
        services.AddTransient<FloatingOrbViewModel>();
        services.AddTransient<SplashScreenViewModel>();
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<OverviewDashboardViewModel>();
        services.AddTransient<CloudDashboardViewModel>();
        services.AddTransient<LocalDashboardViewModel>();
        services.AddTransient<ActionsViewModel>();
        services.AddTransient<ProvidersViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<HistoryViewModel>();
        services.AddTransient<AboutViewModel>();
        
        // Download Management ViewModels
        services.AddTransient<DownloadsPopupViewModel>();
        services.AddTransient<DownloadTaskViewModel>();
        services.AddTransient<AvailableModelViewModel>();
        services.AddTransient<LocalModelViewModel>();

        // Editor/Dialog ViewModels
        services.AddTransient<ActionEditorViewModel>();
        services.AddTransient<CloudProviderEditorViewModel>();
        services.AddTransient<InputDialogViewModel>();
        services.AddTransient<CustomModelImportViewModel>();
        services.AddTransient<ConflictResolutionViewModel>();
        services.AddTransient<ModelLibraryViewModel>();
        services.AddTransient<ManageConnectionViewModel>();
        services.AddTransient<WorkspacePasswordViewModel>();
        services.AddTransient<SyncViewModel>();
        
        // Onboarding ViewModels
        services.AddTransient<OnboardingViewModel>();
        services.AddTransient<CloudOnboardingViewModel>();
        services.AddTransient<TemplateTutorialViewModel>();
        services.AddTransient<HotkeyTutorialViewModel>();
        
        // Injectable Windows
        services.AddTransient<ArcMenuViewModel>();
        services.AddTransient<ArcMenuItemViewModel>();
        services.AddTransient<FloatingOrbWindow>();

        return services;
    }
}