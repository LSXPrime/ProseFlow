using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Microsoft.Extensions.DependencyInjection;
using ProseFlow.Application.DTOs;
using ProseFlow.Application.DTOs.Models;
using ProseFlow.Application.Services;
using ProseFlow.UI.Models;
using ProseFlow.UI.ViewModels.Actions;
using ProseFlow.UI.ViewModels.Dialogs;
using ProseFlow.UI.ViewModels.Downloads;
using ProseFlow.UI.ViewModels.Providers;
using ProseFlow.UI.Views.Actions;
using ProseFlow.UI.Views.Dialogs;
using ShadUI;
using Action = ProseFlow.Core.Models.Action;
using Window = Avalonia.Controls.Window;
using FontWeight = Avalonia.Media.FontWeight;
using Thickness = Avalonia.Thickness;

namespace ProseFlow.UI.Services;

public class DialogService(IServiceProvider serviceProvider) : IDialogService
{
    private static Window? GetWindow(bool topLevel = true)
    {
        return Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            ? topLevel ? desktop.Windows.FirstOrDefault(x => x.IsActive && x != desktop.MainWindow) ?? desktop.MainWindow : desktop.MainWindow
            : null;
    }
    
    /// <inheritdoc />
    public async Task OpenUrlAsync(string url)
    {
        var mainWindow = GetWindow(false);
        if (mainWindow is null) return;
        
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri) && 
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            await mainWindow.Launcher.LaunchUriAsync(new Uri(url));
        }
        else
        {
            var storageItem = (IStorageItem?)await mainWindow.StorageProvider.TryGetFolderFromPathAsync(url) ?? 
                              await mainWindow.StorageProvider.TryGetFileFromPathAsync(url);
            
            if (storageItem != null) await mainWindow.Launcher.LaunchFileAsync(storageItem);
        }
    }
    
    /// <inheritdoc />
    public async Task<string?> ShowOpenFileDialogAsync(string title, string filterName, params string[] filterExtensions)
    {
        var mainWindow = GetWindow(false);
        if (mainWindow is null) return null;

        var result = await mainWindow.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType(filterName) { Patterns = filterExtensions }]
        });

        return result.Count > 0 ? result[0].TryGetLocalPath() : null;
    }
    
    /// <inheritdoc />
    public async Task<string?> ShowOpenFolderDialogAsync(string title)
    {
        var mainWindow = GetWindow(false);
        if (mainWindow is null) return null;

        var result = await mainWindow.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false
        });

        return result.Count > 0 ? result[0].TryGetLocalPath() : null;
    }

    /// <inheritdoc />
    public async Task<string?> ShowSaveFileDialogAsync(string title, string defaultFileName, string filterName, params string[] filterExtensions)
    {
        var mainWindow = GetWindow(false);
        if (mainWindow is null) return null;

        var result = await mainWindow.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = defaultFileName,
            DefaultExtension = filterExtensions.FirstOrDefault()?.TrimStart('*'),
            FileTypeChoices = [new FilePickerFileType(filterName) { Patterns = filterExtensions }]
        });

        return result?.TryGetLocalPath();
    }

    /// <inheritdoc />
    public async Task<bool> ShowActionEditorDialogAsync(Action action)
    {
        var mainWindow = GetWindow(false);
        if (mainWindow is null) return false;
        
        var actionService = serviceProvider.GetRequiredService<ActionManagementService>();

        var editorViewModel = new ActionEditorViewModel(action, actionService, this);
        var editorWindow = new ActionEditorView { DataContext = editorViewModel };

        return await editorWindow.ShowDialog<bool>(mainWindow);
    }

    /// <inheritdoc />
    public void ShowConfirmationDialog(string title, string message, System.Action? onConfirm = null, System.Action? onCancel = null)
    {
        var dialogManager = serviceProvider.GetRequiredService<DialogManager>();
        dialogManager
            .CreateDialog(title, message)
            .WithPrimaryButton("Confirm", onConfirm)
            .WithCancelButton("Cancel", onCancel!)
            .WithMaxWidth(512)
            .Dismissible()
            .Show();
        
    }
    
    /// <inheritdoc />
    public void ShowConfirmationDialogAsync(string title, string message, Func<Task>? onConfirm, Func<Task>? onCancel = null)
    {
        var dialogManager = serviceProvider.GetRequiredService<DialogManager>();
        dialogManager
            .CreateDialog(title, message)
            .WithPrimaryButton("Confirm", onConfirm)
            .WithCancelButton("Cancel", onCancel)
            .WithMaxWidth(512)
            .Dismissible()
            .Show();
        
    }

    /// <inheritdoc />
    public Task<bool> ShowConfirmationDialogAsync(string title, string message)
    {
        var tcs = new TaskCompletionSource<bool>();
        var dialogManager = serviceProvider.GetRequiredService<DialogManager>();
        dialogManager
            .CreateDialog(title, message)
            .WithPrimaryButton("Confirm", () => tcs.TrySetResult(true))
            .WithCancelButton("Cancel", () => tcs.TrySetResult(false))
            .WithMaxWidth(512)
            .Dismissible()
            .Show();
        return tcs.Task;
    }

    /// <inheritdoc />
    public Task<bool> ShowOwnedConfirmationDialogAsync(Window owner, string title, string message)
    {
        var confirmButton = new Button { Content = "Confirm", IsDefault = true };
        var cancelButton = new Button { Content = "Cancel", IsCancel = true };
        confirmButton.Classes.Add("Primary");
        cancelButton.Classes.Add("Outline");

        var dialog = new Window
        {
            Title = title,
            Width = 600,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            SystemDecorations = SystemDecorations.BorderOnly,
            CanResize = false,
            ShowInTaskbar = false,
            CornerRadius = new CornerRadius(8),
            Content = new StackPanel
            {
                Margin = new Thickness(20),
                Spacing = 15,
                Children =
                {
                    new TextBlock { Text = title, FontSize = 18, FontWeight = FontWeight.SemiBold },
                    new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Spacing = 10,
                        Margin = new Thickness(0, 10, 0, 0),
                        Children = { cancelButton, confirmButton }
                    }
                }
            }
        };
        
        confirmButton.Click += (_, _) => dialog.Close(true);
        cancelButton.Click += (_, _) => dialog.Close(false);

        return dialog.ShowDialog<bool>(owner);
    }
    
    /// <inheritdoc />
    public async Task<InputDialogResult> ShowInputDialogAsync(string title, string message, string confirmButtonText, string? initialValue = null)
    {
        var mainWindow = GetWindow();
        if (mainWindow is null) return new InputDialogResult(false, null);
        
        
        var inputViewModel = serviceProvider.GetRequiredService<InputDialogViewModel>();
        inputViewModel.Initialize(title, message, confirmButtonText, initialValue);

        var inputWindow = new InputDialogView { DataContext = inputViewModel };
        _ = inputWindow.ShowDialog(mainWindow);

        var result = await inputViewModel.CompletionSource.Task;
        inputWindow.Close();
        
        return result;
    }

    /// <inheritdoc />
    public Task ShowModelLibraryDialogAsync()
    {
        var tcs = new TaskCompletionSource();
        
        var dialogManager = serviceProvider.GetRequiredService<DialogManager>();
        var modelLibraryViewModel = serviceProvider.GetRequiredService<ModelLibraryViewModel>();
        _ = modelLibraryViewModel.OnNavigatedToAsync();
        dialogManager
            .CreateDialog(modelLibraryViewModel)
            .Dismissible()
            .WithMinWidth(900).WithMaxWidth(900)
            .WithSuccessCallback(vm =>
            {
                vm.OnClosing();
                tcs.SetResult();
            })
            .WithCancelCallback(() => tcs.SetResult())
            .Show();
        
        return tcs.Task;
    }
    
    /// <inheritdoc />
    public void ShowDownloadsDialog()
    {
        var dialogManager = serviceProvider.GetRequiredService<DialogManager>();
        var downloadsViewModel = serviceProvider.GetRequiredService<DownloadsPopupViewModel>();
        dialogManager
            .CreateDialog(downloadsViewModel)
            .Dismissible()
            .WithMinWidth(600)
            .Show();
    }
    
    /// <inheritdoc />
    public async Task<CustomModelImportData?> ShowImportModelDialogAsync()
    {
        var mainWindow = GetWindow();
        if (mainWindow is null) return null;

        var importViewModel = serviceProvider.GetRequiredService<CustomModelImportViewModel>();
        var importWindow = new CustomModelImportView { DataContext = importViewModel };
        
        _ = importWindow.ShowDialog(mainWindow);
        return await importViewModel.CompletionSource.Task;
    }

    /// <inheritdoc />
    public async Task<List<ActionConflict>?> ShowConflictResolutionDialogAsync(List<ActionConflict> conflicts)
    {
        var mainWindow = GetWindow();
        if (mainWindow is null) return null;

        var vm = serviceProvider.GetRequiredService<ConflictResolutionViewModel>();
        vm.Initialize(conflicts);

        var dialog = new ConflictResolutionDialog { DataContext = vm };
        
        _ = dialog.ShowDialog(mainWindow);
        return await vm.CompletionSource.Task;
    }

    /// <inheritdoc />
    public async Task<bool> ShowCriticalConfirmationDialogAsync(string title, string message, string confirmText, string cancelText)
    {
        // Native blocking window for startup-critical error.
        var window = new Window
        {
            Title = title,
            Width = 450,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            SystemDecorations = SystemDecorations.Full,
            CanResize = false,
            ShowInTaskbar = true
        };
        
        var tcs = new TaskCompletionSource<bool>();


        var confirmButton = new Button { Content = confirmText, IsDefault = true };
        var cancelButton = new Button { Content = cancelText, IsCancel = true };

        confirmButton.Click += (_, _) =>
        {
            tcs.SetResult(true);
            window.Close();
        };
        cancelButton.Click += (_, _) =>
        {
            tcs.SetResult(false);
            window.Close();
        };

        window.Content = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 15,
            Children =
            {
                new TextBlock { Text = title, FontSize = 18, FontWeight = FontWeight.SemiBold },
                new TextBlock
                {
                    Text = message,
                    TextWrapping = TextWrapping.Wrap
                },
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Spacing = 10,
                    Margin = new Thickness(0, 10, 0, 0),
                    Children = { cancelButton, confirmButton }
                }
            }
        };
        
        window.Show();
        window.Activate();

        return await tcs.Task;
    }
    
    /// <inheritdoc />
    public async Task<bool> ShowManageConnectionDialogAsync()
    {
        var mainWindow = GetWindow();
        if (mainWindow is null) return false;

        var vm = serviceProvider.GetRequiredService<ManageConnectionViewModel>();
        var window = new ManageConnectionWindow { DataContext = vm };
        
        _ = window.ShowDialog(mainWindow);
        return await vm.CompletionSource.Task;
    }

    /// <inheritdoc />
    public async Task<WorkspacePasswordResult> ShowWorkspacePasswordDialogAsync(WorkspacePasswordMode mode)
    {
        var mainWindow = GetWindow();
        if (mainWindow is null) return new WorkspacePasswordResult(false, null);
        
        var vm = serviceProvider.GetRequiredService<WorkspacePasswordViewModel>();
        vm.Initialize(mode);
        
        var window = new WorkspacePasswordWindow { DataContext = vm };
        
        _ = window.ShowDialog(mainWindow);
        return await vm.CompletionSource.Task;
    }
    
    /// <inheritdoc />
    public async Task<bool> ShowSyncDialogAsync()
    {
        var mainWindow = GetWindow();
        if (mainWindow is null) return false;
        
        var vm = serviceProvider.GetRequiredService<SyncViewModel>();
        var window = new SyncWindow { DataContext = vm };
        
        _ = window.ShowDialog(mainWindow);
        return await vm.CompletionSource.Task;
    }
}