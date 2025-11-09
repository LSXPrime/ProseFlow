using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using CommunityToolkit.Mvvm.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ProseFlow.UI.Services.Startup;
using ProseFlow.UI.ViewModels.Windows;
using ProseFlow.UI.Views;
using ProseFlow.UI.Views.Windows;
using Velopack;

namespace ProseFlow.UI;

public class App : Avalonia.Application
{
    public IServiceProvider? Services { get; private set; }
    private TrayIcon? _trayIcon;

    public override void Initialize()
    {
        VelopackApp.Build().Run();
        AvaloniaXamlLoader.Load(this);
    }

    public override async void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
        {
            base.OnFrameworkInitializationCompleted();
            return;
        }
        
        // Create main window; it acts as an owner for startup dialogs.
        desktop.MainWindow = new MainWindow();

        // 1. Show the splash screen immediately to provide instant feedback.
        var splashViewModel = new SplashScreenViewModel();
        var splashScreenView = new SplashScreenWindow { DataContext = splashViewModel, Topmost = true };
        splashScreenView.Show();
        splashScreenView.Activate();
        await Task.Delay(10); // Force a UI update to ensure the splash screen is rendered
        
        // 2. Configure DI and initialize the database with a resilient recovery loop.
        Services = await DatabaseInitializer.InitializeWithServicesAsync(desktop, splashViewModel);
        
        // If Services is null, the user chose to quit during database recovery.
        if (Services is null)
        {
            desktop.Shutdown();
            return;
        }
        Ioc.Default.ConfigureServices(Services);
        var logger = Services.GetRequiredService<ILogger<App>>();
        logger.LogInformation("Startup orchestration initiated.");

        // 3. Initialize background services.
        await BackgroundServiceInitializer.StartServicesAsync(Services, splashViewModel);

        // 4. Set up the main UI components.
        UiInitializer.RegisterDialogs(Services);
        UiInitializer.SubscribeToAppEvents(Services);
        UiInitializer.SetupMainWindow(desktop, Services, (s, e) => ApplicationCleanup.OnExit(s, e, Services));
        _trayIcon = UiInitializer.CreateTrayIcon(Services, desktop);
        if (_trayIcon is not null) TrayIcon.SetIcons(this, [_trayIcon]);

        // 5. Finalize startup and close splash screen.
        splashViewModel.Report("Finalizing...");
        await Task.Delay(500); // Allow user to see final message
        splashScreenView.Close();

        // 6. Run post-startup logic (Onboarding or normal application start).
        await PostStartupOrchestrator.RunAsync(desktop, Services);

        base.OnFrameworkInitializationCompleted();
    }
}