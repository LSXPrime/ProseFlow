using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ProseFlow.Application.DTOs;
using ProseFlow.Application.Events;
using ProseFlow.Application.Services;
using ProseFlow.Core.Enums;
using ProseFlow.Core.Interfaces.Os;
using ProseFlow.UI.ViewModels.Windows;
using ProseFlow.UI.Views.Windows;

namespace ProseFlow.UI.Services;

/// <summary>
/// Manages the lifecycle and state of the FloatingOrbWindow, ensuring it responds to
/// application events and user interactions correctly.
/// </summary>
public class FloatingOrbService(
    IServiceProvider serviceProvider, 
    ILogger<FloatingOrbService> logger, 
    ActionManagementService actionService,
    IActiveWindowService activeWindowService)
{
    private FloatingOrbWindow? _orbWindow;
    private ArcMenuView? _arcMenuWindow;
    private bool _isFeatureEnabled;

    /// <summary>
    /// Initializes the service, subscribing to necessary application events.
    /// Should be called once at application startup.
    /// </summary>
    public void Initialize()
    {
        AppEvents.FloatingMenuStateChanged += OnFloatingMenuStateChanged;
        AppEvents.ActionProcessingStateChanged += OnActionProcessingStateChanged;
        logger.LogInformation("FloatingOrbService initialized and subscribed to application events.");
    }

    private void OnActionProcessingStateChanged(ActionProcessingState state)
    {
        Dispatcher.UIThread.Post(() =>
        {
            FlashActionProcessingState(state);
        });
        
    }

    /// <summary>
    /// Enables or disables the floating orb feature based on user settings.
    /// This method will show or hide the orb accordingly.
    /// </summary>
    /// <param name="isEnabled">True to enable and show the orb; false to disable and hide it.</param>
    public void SetEnabled(bool isEnabled)
    {
        _isFeatureEnabled = isEnabled;
        if (_isFeatureEnabled)
            ShowOrb();
        else
            HideOrb();
    }

    /// <summary>
    /// Handles the showing and hiding of the orb when the main action menu opens or closes.
    /// </summary>
    private void OnFloatingMenuStateChanged(bool isMenuOpen)
    {
        if (isMenuOpen)
            HideOrb();
        else
            ShowOrb();
    }
    
    /// <summary>
    /// Orchestrates the display of the Arc Menu when text is dropped onto the Orb.
    /// </summary>
    /// <param name="droppedText">The text content dropped by the user.</param>
    public async Task ShowArcMenuForDroppedTextAsync(string droppedText)
    {
        if (_orbWindow is null) return;
        
        try
        {
            var appContext = await activeWindowService.GetActiveWindowProcessNameAsync();
            var relevantActions = await actionService.GetRelevantActionsAsync(appContext);

            if (relevantActions.Count == 0)
            {
                AppEvents.RequestNotification("No relevant actions found. Please favorite some actions to use this feature.", NotificationType.Info);
                return;
            }

            var viewModel = serviceProvider.GetRequiredService<ArcMenuViewModel>();
            viewModel.Initialize(relevantActions);

            _arcMenuWindow = new ArcMenuView { DataContext = viewModel };
            _arcMenuWindow.Show(_orbWindow);

            var selectedAction = await viewModel.CompletionSource.Task;
            
            if (selectedAction is not null)
            {
                var executionRequest = new ActionExecutionRequest(selectedAction, OutputMode.Default, null);
                
                var orchestrationService = serviceProvider.GetRequiredService<ActionOrchestrationService>();
                _ = Task.Run(() => orchestrationService.ProcessRequestAsync(executionRequest, false, droppedText));
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to show or process the Arc Menu.");
            AppEvents.RequestNotification("An error occurred while showing the action menu.", NotificationType.Error);
        }
        
        _arcMenuWindow?.Hide();
    }
    
    /// <summary>
    /// Shows the floating orb on the screen.
    /// </summary>
    public void ShowOrb()
    {
        // Only show the orb if the feature is globally enabled.
        if (!_isFeatureEnabled) return;

        Dispatcher.UIThread.Post(() =>
        {
            try
            {
                if (_orbWindow is null)
                {
                    _orbWindow = serviceProvider.GetRequiredService<FloatingOrbWindow>();
                    _orbWindow.PositionChanged += OnOrbPositionChanged;
                    
                    // Default position in the top-right corner of the primary screen.
                    if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow?.Screens.Primary != null)
                    {
                        var screenWidth = desktop.MainWindow.Screens.Primary.WorkingArea.Width;
                        var screenHeight = desktop.MainWindow.Screens.Primary.WorkingArea.Height;
                        _orbWindow.Position = new PixelPoint(screenWidth - 128, screenHeight / 2);
                    }
                }
                _orbWindow.Focusable = false;
                _orbWindow.ShowActivated = false;
                _orbWindow.Show();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to create or show the FloatingOrbWindow.");
            }
        });
    }

    /// <summary>
    /// Hides the floating orb from the screen.
    /// </summary>
    public void HideOrb()
    {
        Dispatcher.UIThread.Post(() =>
        {
            _arcMenuWindow?.Close(); // Also close the arc menu if it's open
            _orbWindow?.Hide();
        });
    }

    /// <summary>
    /// Updates the visual state of the Orb.
    /// </summary>
    private void UpdateActionProcessingState(ActionProcessingState state)
    {
        if (_orbWindow?.DataContext is FloatingOrbViewModel vm)
        {
            Dispatcher.UIThread.Post(() => vm.State = state);
        }
    }

    /// <summary>
    /// Briefly flashes a state (like Success or Error) and then returns to Idle.
    /// </summary>
    private void FlashActionProcessingState(ActionProcessingState state)
    {
        UpdateActionProcessingState(state);
        Task.Delay(1500).ContinueWith(_ =>
        {
            Dispatcher.UIThread.Post(() => UpdateActionProcessingState(ActionProcessingState.Idle));
        });
    }

    /// <summary>
    /// Handles the screen-edge snapping logic when the user finishes dragging the Orb.
    /// </summary>
    private void OnOrbPositionChanged(object? sender, PixelPointEventArgs e)
    {
        if (_orbWindow is null || _orbWindow.IsDragging) return;

        var screen = _orbWindow.Screens.ScreenFromPoint(_orbWindow.Position);
        if (screen is null) return;
        
        var workingArea = screen.WorkingArea;
        var orbCenter = new Point(e.Point.X + _orbWindow.Width / 2, e.Point.Y + _orbWindow.Height / 2);

        // Distances to each edge
        var distLeft = orbCenter.X - workingArea.X;
        var distRight = workingArea.Right - orbCenter.X;
        var distTop = orbCenter.Y - workingArea.Y;
        var distBottom = workingArea.Bottom - orbCenter.Y;

        var minDist = Math.Min(Math.Min(distLeft, distRight), Math.Min(distTop, distBottom));

        var margin = 16;
        var finalPos = _orbWindow.Position;

        if (Math.Abs(minDist - distTop) < 10)
            finalPos = finalPos.WithY(workingArea.Y + margin);
        else if (Math.Abs(minDist - distBottom) < 10)
            finalPos = finalPos.WithY(workingArea.Bottom - (int)_orbWindow.Height - margin);
        else if (Math.Abs(minDist - distLeft) < 10)
            finalPos = finalPos.WithX(workingArea.X + margin);
        else // distRight
            finalPos = finalPos.WithX(workingArea.Right - (int)_orbWindow.Width - margin);

        _orbWindow.Position = finalPos;
    }
}