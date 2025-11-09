using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls.ApplicationLifetimes;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ProseFlow.Infrastructure.Data;
using ProseFlow.UI.ViewModels.Windows;
using ProseFlow.UI.Views.Windows;

namespace ProseFlow.UI.Services.Startup;

/// <summary>
/// Handles the initialization and potential recovery of the application database.
/// </summary>
public static class DatabaseInitializer
{
    /// <summary>
    /// Attempts to configure services and initialize the database, providing a recovery path for database corruption.
    /// </summary>
    /// <param name="desktop">The application lifetime instance.</param>
    /// <param name="splashViewModel">The view model for the splash screen to report progress.</param>
    /// <returns>A configured <see cref="IServiceProvider"/> on success, or null if the user cancels the process.</returns>
    public static async Task<IServiceProvider?> InitializeWithServicesAsync(
        IClassicDesktopStyleApplicationLifetime desktop, SplashScreenViewModel splashViewModel)
    {
        var isInitialized = false;

        while (!isInitialized)
        {
            splashViewModel.Report("Configuring services...");
            var serviceProvider = DependencyInjectionSetup.ConfigureServices();
            var logger = serviceProvider.GetRequiredService<ILogger<App>>();

            try
            {
                // Attempt to initialize the database.
                splashViewModel.Report("Initializing database...");
                await using (var scope = serviceProvider.CreateAsyncScope())
                {
                    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    await dbContext.Database.MigrateAsync();
                }

                isInitialized = true;
                logger.LogInformation("Database and services initialized successfully.");
                return serviceProvider; // Return the successful provider
            }
            catch (SqliteException ex)
            {
                logger.LogCritical(ex, "Database initialization failed due to a SQLite error (Code: {ErrorCode}). Prompting user for action.", ex.SqliteErrorCode);
                
                // Hide splash screen before showing critical error dialog.
                if (desktop.Windows.Count > 0 && desktop.Windows.FirstOrDefault(x => x is SplashScreenWindow) is SplashScreenWindow splash)
                    splash.Close();

                // The failed ServiceProvider must be disposed to release its hold on services.
                (serviceProvider as IDisposable)?.Dispose();

                // Clear all SQLite connection pools to ensure no locks are held.
                SqliteConnection.ClearAllPools();

                // Force garbage collection to release any lingering unmanaged resources.
                GC.Collect();
                GC.WaitForPendingFinalizers();

                // Create a temporary, dependency-free service to show the critical dialog.
                var tempDialogService = new DialogService(null!); 
                desktop.MainWindow?.Show();
                desktop.MainWindow?.Activate();
                var userWantsToReset = await tempDialogService.ShowCriticalConfirmationDialogAsync(
                    "Database Error",
                    "ProseFlow's data file is corrupted or inaccessible. To continue, the application must reset its data. This will erase all your settings, actions, and history. A backup of the corrupted file will be made.",
                    "Backup & Reset",
                    "Quit"
                );

                if (userWantsToReset)
                {
                    if (!BackupAndRemoveDatabase(logger))
                    {
                        await tempDialogService.ShowCriticalConfirmationDialogAsync("Fatal Error", "Could not remove the corrupted database file. Please find it in the application's data folder and delete it manually. The application will now exit.", "OK", "Close");
                        return null; // Fatal error, signal to shut down.
                    }
                    
                    // Re-show the splash screen for the next initialization attempt.
                    var newSplashScreen = new SplashScreenWindow { DataContext = splashViewModel };
                    if (desktop.MainWindow != null) newSplashScreen.Show(desktop.MainWindow);
                }
                else // User chose to quit.
                {
                    return null; // Signal to shut down.
                }
            }
        }

        return null; // Should not be reached, but required for compiler.
    }

    private static bool BackupAndRemoveDatabase(ILogger logger)
    {
        try
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var proseFlowDataPath = Path.Combine(appDataPath, "ProseFlow");
            var dbPath = Path.Combine(proseFlowDataPath, "proseflow.db");
            if (File.Exists(dbPath))
            {
                var backupPath = Path.Combine(proseFlowDataPath, $"proseflow.db.corrupt-{DateTime.Now:yyyyMMddHHmmss}.bak");
                File.Move(dbPath, backupPath, true);
                logger.LogInformation("Corrupted database backed up to {BackupPath}", backupPath);
            }
            return true;
        }
        catch (Exception backupEx)
        {
            logger.LogError(backupEx, "Failed to back up and remove the corrupted database after cleanup.");
            return false;
        }
    }
}