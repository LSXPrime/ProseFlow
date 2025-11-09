using System;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using ProseFlow.Application.Events;
using ProseFlow.Application.Interfaces;
using ProseFlow.Application.Services;
using ProseFlow.Infrastructure.Services.AiProviders.Local;
using ProseFlow.UI.ViewModels;
using ProseFlow.UI.ViewModels.Dialogs;
using ProseFlow.UI.ViewModels.Downloads;
using ProseFlow.UI.ViewModels.Providers;
using ProseFlow.UI.ViewModels.Windows;
using ProseFlow.UI.Views.Dialogs;
using ProseFlow.UI.Views.Downloads;
using ProseFlow.UI.Views.Providers;
using ProseFlow.UI.Views.Windows;
using ShadUI;

namespace ProseFlow.UI.Services.Startup;

/// <summary>
/// Initializes and configures all UI-related components of the application.
/// </summary>
public static class UiInitializer
{
    /// <summary>
    /// Configures the main window's properties, events, and data context.
    /// </summary>
    public static void SetupMainWindow(IClassicDesktopStyleApplicationLifetime desktop, IServiceProvider services, EventHandler<ControlledApplicationLifetimeExitEventArgs> onExit)
    {
        // Don't shut down the app when the main window is closed.
        desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        desktop.Exit += onExit;

        var generalSettings = services.GetRequiredService<SettingsService>().GetGeneralSettingsAsync().Result;
        
        desktop.MainWindow!.RequestedThemeVariant = generalSettings.Theme switch
        {
            "Light" => ThemeVariant.Light,
            "Dark" => ThemeVariant.Dark,
            _ => ThemeVariant.Default
        };
        
        // Assign the main view model now, so the window is ready.
        desktop.MainWindow.DataContext = services.GetRequiredService<MainViewModel>();
        
        // Handle the closing event to hide the window instead of closing
        desktop.MainWindow.Closing += (_, e) =>
        {
            e.Cancel = true;
            desktop.MainWindow.Hide();
            AppEvents.OnMainWindowVisibilityChanged(false);
        };
    }
    
    /// <summary>
    /// Registers view-to-viewmodel mappings for the dialog manager.
    /// </summary>
    public static void RegisterDialogs(IServiceProvider services)
    {
        var dialogManager = services.GetRequiredService<DialogManager>();
        dialogManager.Register<InputDialogView, InputDialogViewModel>();
        dialogManager.Register<ModelLibraryView, ModelLibraryViewModel>();
        dialogManager.Register<DownloadsPopupView, DownloadsPopupViewModel>();
    }

    /// <summary>
    /// Subscribes UI handlers to application-layer events.
    /// </summary>
    public static void SubscribeToAppEvents(IServiceProvider services)
    {
        var notificationService = services.GetRequiredService<NotificationService>();
        var dialogService = services.GetRequiredService<IDialogService>();

        AppEvents.ShowNotificationRequested += (message, type) =>
            Dispatcher.UIThread.Post(() => notificationService.Show(message, type));

        AppEvents.ShowResultWindowAndAwaitRefinement += data =>
        {
            // This must be run on the UI thread.
            return Dispatcher.UIThread.InvokeAsync(async () =>
            {
                var viewModel = new ResultViewModel(data);
                var window = new ResultWindow
                {
                    DataContext = viewModel,
                    Focusable = true,
                    WindowStartupLocation = WindowStartupLocation.CenterScreen,
                    WindowState = WindowState.Normal,
                };
                window.Show();
                return await viewModel.CompletionSource.Task;
            });
        };

        AppEvents.ShowDiffViewRequested += data =>
        {
            return Dispatcher.UIThread.InvokeAsync(async () =>
            {
                var viewModel = new DiffViewModel(data);
                var window = new DiffViewWindow
                {
                    DataContext = viewModel,
                    Focusable = true,
                    WindowStartupLocation = WindowStartupLocation.CenterScreen,
                    WindowState = WindowState.Normal,
                };
                window.Show();
                return await viewModel.CompletionSource.Task;
            });
        };

        AppEvents.ShowFloatingMenuRequested += async (actions, context, isGenerationMode) =>
        {
            var providerSettings = await services.GetRequiredService<SettingsService>().GetProviderSettingsAsync();
            var templateEngine = services.GetRequiredService<TemplateEngineService>();
            var documentReader = services.GetRequiredService<IDocumentReaderService>();
            var viewModel = new FloatingActionMenuViewModel(actions, providerSettings, context, templateEngine, documentReader, isGenerationMode);
            Dispatcher.UIThread.Post(() =>
            {
                var window = new FloatingActionMenuWindow
                {
                    DataContext = viewModel,
                    ShowActivated = true,
                    Topmost = true,
                    Focusable = true,
                    WindowStartupLocation = WindowStartupLocation.CenterScreen,
                    WindowState = WindowState.Normal,
                };
                window.Show();
            });

            return await viewModel.WaitForSelectionAsync();
        };

        AppEvents.ResolveConflictsRequested += conflicts => Dispatcher.UIThread.InvokeAsync(() => dialogService.ShowConflictResolutionDialogAsync(conflicts));
    }
    
    /// <summary>
    /// Creates and configures the application's system tray icon.
    /// </summary>
    public static TrayIcon CreateTrayIcon(IServiceProvider services, IClassicDesktopStyleApplicationLifetime desktop)
    {
        var trayVm = services.GetRequiredService<TrayIconViewModel>();
        var mainVm = services.GetRequiredService<MainViewModel>();

        // Wire up the event to show the main window
        trayVm.ShowMainWindowRequested += () =>
        {
            // Ensure we're on the UI thread before showing the window
            Dispatcher.UIThread.Post(() =>
            {
                desktop.MainWindow?.Show();
                desktop.MainWindow?.Activate();
                AppEvents.OnMainWindowVisibilityChanged(true);
            });
        };

        trayVm.ShowDownloadsRequested += () =>
        {
             Dispatcher.UIThread.Post(() =>
             {
                 desktop.MainWindow?.Show();
                 desktop.MainWindow?.Activate();
                 mainVm.ShowDownloadsPopupCommand.Execute(null);
                 AppEvents.OnMainWindowVisibilityChanged(true);
             });
        };

        // Define a converter for the menu item header
        var modelStatusToHeaderConverter = new FuncValueConverter<bool, string>(isLoaded =>
            isLoaded ? "Unload Local Model" : "Load Local Model");

        var providerTypeToHeaderConverter = new FuncValueConverter<string, string>(providerType =>
            $"Set Primary Provider ({providerType})");
        
        var downloadCountToHeaderConverter = new FuncValueConverter<int, string>(count => $"Downloads ({count})");

        // Build the context menu items
        var openItem = new NativeMenuItem { Header = "Open ProseFlow", Command = trayVm.OpenSettingsCommand };

        var toggleModelItem = new NativeMenuItem { Command = trayVm.ToggleLocalModelCommand };
        toggleModelItem.Bind(NativeMenuItem.HeaderProperty, new Binding(nameof(trayVm.IsModelLoaded))
            { Source = trayVm, Converter = modelStatusToHeaderConverter });
        toggleModelItem.Bind(NativeMenuItem.IsEnabledProperty, new Binding(nameof(trayVm.ManagerStatus))
            { Source = trayVm, Converter = new FuncValueConverter<ModelStatus, bool>(s => s != ModelStatus.Loading) });
        
        var downloadsItem = new NativeMenuItem { Command = trayVm.ShowDownloadsCommand };
        downloadsItem.Bind(NativeMenuItem.HeaderProperty,
            new Binding(nameof(trayVm.ActiveDownloadCount)) { Source = trayVm, Converter = downloadCountToHeaderConverter });
        downloadsItem.Bind(NativeMenuItem.IsVisibleProperty,
            new Binding(nameof(trayVm.HasActiveDownloads)) { Source = trayVm });

        // Provider Type Sub-menu
        var cloudProviderItem = new NativeMenuItem { Header = "Cloud", Command = trayVm.SetProviderTypeCommand, CommandParameter = "Cloud" };
        cloudProviderItem.Bind(NativeMenuItem.IsCheckedProperty,
            new Binding(nameof(trayVm.CurrentProviderType)) { Source = trayVm, Converter = new FuncValueConverter<string, bool>(t => t == "Cloud") });

        var localProviderItem = new NativeMenuItem { Header = "Local", Command = trayVm.SetProviderTypeCommand, CommandParameter = "Local" };
        localProviderItem.Bind(NativeMenuItem.IsCheckedProperty,
            new Binding(nameof(trayVm.CurrentProviderType)) { Source = trayVm, Converter = new FuncValueConverter<string, bool>(t => t == "Local") });

        var setProviderSubMenu = new NativeMenuItem { Menu = new NativeMenu { Items = { cloudProviderItem, localProviderItem } } };
        setProviderSubMenu.Bind(NativeMenuItem.HeaderProperty, new Binding(nameof(trayVm.CurrentProviderType)) { Source = trayVm, Converter = providerTypeToHeaderConverter });

        var quitItem = new NativeMenuItem { Header = "Quit", Command = trayVm.QuitApplicationCommand };

        // Create the TrayIcon instance
        var trayIcon = new TrayIcon
        {
            Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://ProseFlow/Assets/logo.ico"))),
            ToolTipText = "ProseFlow",
            Menu = new NativeMenu
            {
                Items =
                {
                    openItem, new NativeMenuItemSeparator(), toggleModelItem, setProviderSubMenu,
                    new NativeMenuItemSeparator(), downloadsItem, new NativeMenuItemSeparator(), quitItem
                }
            }
        };

        // Open settings on left-click
        trayIcon.Clicked += (_, _) => trayVm.OpenSettingsCommand.Execute(null);

        return trayIcon;
    }
}