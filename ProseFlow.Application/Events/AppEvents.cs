using ProseFlow.Application.DTOs;
using ProseFlow.Core.Enums;
using Action = ProseFlow.Core.Models.Action;

namespace ProseFlow.Application.Events;

public enum NotificationType { Info, Success, Warning, Error }

public static class AppEvents
{
    /// <summary>
    /// Configuration flag to enable or disable the floating menu feature globally.
    /// </summary>
    public static bool IsShowFloatingMenuEnabled = true;

    /// <summary>
    /// Configuration flag to enable or disable the result window display feature globally.
    /// </summary>
    public static bool IsShowResultWindowEnabled = true;

    /// <summary>
    /// Configuration flag to enable or disable system notifications (toasts) globally.
    /// </summary>
    public static bool IsShowNotificationEnabled = true;
    
    /// <summary>
    /// Configuration flag to enable or disable the conflict resolution UI globally.
    /// </summary>
    public static bool IsResolveConflictsEnabled = true;

    /// <summary>
    /// Raised when an AI action processing state changes.
    /// </summary>
    public static event Action<ActionProcessingState>? ActionProcessingStateChanged;

    public static void OnActionProcessingStarted(ActionProcessingState state) => ActionProcessingStateChanged?.Invoke(state);
    
    /// <summary>
    /// Raised when the main application window's visibility changes (e.g., minimized, hidden, shown).
    /// The boolean payload is true if the window is visible, and false otherwise.
    /// </summary>
    public static event Action<bool>? MainWindowVisibilityChanged;
    
    /// <summary>
    /// Invokes the MainWindowVisibilityChanged event.
    /// </summary>
    /// <param name="isVisible">True if the main window is now visible; otherwise, false.</param>
    public static void OnMainWindowVisibilityChanged(bool isVisible) => MainWindowVisibilityChanged?.Invoke(isVisible);

    /// <summary>
    /// Raised just before the Floating Action Menu is shown or closed.
    /// Useful for hiding other UI elements, like the floating button.
    /// </summary>
    public static event Action<bool>? FloatingMenuStateChanged;
    
    /// <summary>
    /// Invokes the FloatingMenuStateChanged event.
    /// </summary>
    public static void OnFloatingMenuStateChanged(bool isOpen) => FloatingMenuStateChanged?.Invoke(isOpen);
    
    /// <summary>
    /// Raised when the Action Orchestration Service needs the UI to display the Floating Action Menu.
    /// The UI layer subscribes to this, shows the menu, and returns the user's selection.
    /// The Func returns a task that resolves to the user's choice, or null if cancelled.
    /// </summary>
    public static event Func<IEnumerable<Action>, string, bool, Task<ActionExecutionRequest?>>? ShowFloatingMenuRequested;

    /// <summary>
    /// Invokes the ShowFloatingMenuRequested event.
    /// </summary>
    /// <param name="availableActions">The list of actions to display.</param>
    /// <param name="activeAppContext">The context of the active application.</param>
    /// <param name="isGenerationMode">True if the menu should be in 'generate from scratch' mode.</param>
    public static async Task<ActionExecutionRequest?> RequestFloatingMenuAsync(IEnumerable<Action> availableActions, string activeAppContext, bool isGenerationMode)
    {
        if (!IsShowFloatingMenuEnabled) return null;

        return ShowFloatingMenuRequested is not null
            ? await ShowFloatingMenuRequested.Invoke(availableActions, activeAppContext, isGenerationMode)
            : await Task.FromResult<ActionExecutionRequest?>(null);
    }
    
    /// <summary>
    /// Raised when a result needs to be displayed in a window.
    /// The UI subscribes and is responsible for showing the window and then returning a Task
    /// that completes with the user's next action (a refinement instruction or null if closed).
    /// </summary>
    public static event Func<ResultWindowData, Task<RefinementRequest?>>? ShowResultWindowAndAwaitRefinement;

    /// <summary>
    /// Invokes the event to show the result window and waits for the user's interaction.
    /// </summary>
    /// <returns>A RefinementRequest if the user wants to refine, otherwise null.</returns>
    public static async Task<RefinementRequest?> RequestResultWindowAsync(ResultWindowData data)
    {
        if (!IsShowResultWindowEnabled) return null;

        return ShowResultWindowAndAwaitRefinement is not null
            ? await ShowResultWindowAndAwaitRefinement.Invoke(data)
            : await Task.FromResult<RefinementRequest?>(null);
    }
    
    /// <summary>
    /// Raised when a diff view needs to be displayed.
    /// The UI subscribes, shows the diff window, and returns a task that completes
    /// with the user's decision (Accept, Refine, Regenerate, or Cancel).
    /// </summary>
    public static event Func<DiffViewData, Task<DiffViewResult?>>? ShowDiffViewRequested;

    /// <summary>
    /// Invokes the event to show the diff view window and waits for user interaction.
    /// </summary>
    /// <returns>A DiffViewResult representing the user's choice, or null if the UI handler isn't attached.</returns>
    public static async Task<DiffViewResult?> RequestDiffViewAsync(DiffViewData data)
    {
        if (!IsShowResultWindowEnabled) return null; 

        return ShowDiffViewRequested is not null
            ? await ShowDiffViewRequested.Invoke(data)
            : await Task.FromResult<DiffViewResult?>(null);
    }

    /// <summary>
    /// Raised to show a system notification (toast).
    /// The UI layer subscribes to this to display feedback.
    /// </summary>
    public static event Action<string, NotificationType>? ShowNotificationRequested;

    /// <summary>
    /// Invokes the ShowNotificationRequested event.
    /// </summary>
    public static void RequestNotification(string message, NotificationType type)
    {
        if (!IsShowNotificationEnabled) return;

        ShowNotificationRequested?.Invoke(message, type);
    }

    /// <summary>
    /// Raised when the Action Management Service detects conflicts during an import.
    /// The UI layer subscribes to this, shows a resolution dialog, and returns the user's choices.
    /// </summary>
    public static event Func<List<ActionConflict>, Task<List<ActionConflict>?>>? ResolveConflictsRequested;

    /// <summary>
    /// Invokes the ResolveConflictsRequested event to get user input for import conflicts.
    /// </summary>
    /// <returns>A list of resolved conflicts, or null if the user cancelled the operation.</returns>
    public static async Task<List<ActionConflict>?> RequestConflictResolutionAsync(List<ActionConflict> conflicts)
    {
        if (!IsResolveConflictsEnabled) return null;

        return ResolveConflictsRequested is not null
            ? await ResolveConflictsRequested.Invoke(conflicts)
            : await Task.FromResult<List<ActionConflict>?>(null);
    }
}