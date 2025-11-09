using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ProseFlow.Application.DTOs;
using ProseFlow.Application.Events;
using ProseFlow.Core.Enums;
using ProseFlow.Core.Interfaces;
using ProseFlow.Core.Models;
using Action = ProseFlow.Core.Models.Action;

namespace ProseFlow.Application.Services;

/// <summary>
/// Manages CRUD operations and business logic for Actions and ActionGroups.
/// </summary>
public class ActionManagementService(IServiceScopeFactory scopeFactory, ILogger<ActionManagementService> logger)
{
    #region ActionGroup Management

    public Task<List<ActionGroup>> GetActionGroupsWithActionsAsync()
    {
        return ExecuteQueryAsync(unitOfWork => unitOfWork.ActionGroups.GetAllOrderedWithActionsAsync());
    }

    public Task<List<ActionGroup>> GetActionGroupsAsync()
    {
        return ExecuteQueryAsync(unitOfWork => unitOfWork.ActionGroups.GetAllOrderedAsync());
    }

    public Task CreateActionGroupAsync(ActionGroup group)
    {
        return ExecuteCommandAsync(async unitOfWork =>
        {
            var maxSortOrder = await unitOfWork.ActionGroups.GetMaxSortOrderAsync();
            group.SortOrder = maxSortOrder + 1;
            await unitOfWork.ActionGroups.AddAsync(group);
        });
    }

    public Task UpdateActionGroupAsync(ActionGroup group)
    {
        return ExecuteCommandAsync(unitOfWork =>
        {
            unitOfWork.ActionGroups.Update(group);
            return Task.CompletedTask;
        });
    }

    public Task DeleteActionGroupAsync(int groupId)
    {
        return ExecuteCommandAsync(async unitOfWork =>
        {
            // The default group (ID=1) cannot be deleted.
            if (groupId == 1)
            {
                logger.LogWarning("Attempted to delete the default 'General' group.");
                return;
            }

            var groupToDelete = await unitOfWork.ActionGroups.GetByIdAsync(groupId);
            if (groupToDelete is null) return;

            var defaultGroup = await unitOfWork.ActionGroups.GetDefaultGroupAsync() ??
                               throw new InvalidOperationException("Default 'General' group not found.");

            // Re-parent all actions from the deleted group to the default group.
            var actionsToMove = await unitOfWork.Actions.GetByExpressionAsync(a => a.ActionGroupId == groupId);
            foreach (var action in actionsToMove)
            {
                action.ActionGroupId = defaultGroup.Id;
                unitOfWork.Actions.Update(action);
            }

            unitOfWork.ActionGroups.Delete(groupToDelete);
        });
    }

    public Task UpdateActionGroupOrderAsync(List<ActionGroup> orderedGroups)
    {
        return ExecuteCommandAsync(unitOfWork => unitOfWork.ActionGroups.UpdateOrderAsync(orderedGroups));
    }

    #endregion

    #region Action Management

    public Task<List<Action>> GetActionsAsync()
    {
        return ExecuteQueryAsync(unitOfWork => unitOfWork.Actions.GetAllOrderedAsync());
    }

    public Task UpdateActionAsync(Action action)
    {
        return ExecuteCommandAsync(async unitOfWork =>
        {
            var existingAction = await unitOfWork.Actions.GetByIdWithPlaceholdersAsync(action.Id);
            if (existingAction is null)
            {
                logger.LogWarning("Attempted to update a non-existent action with ID {ActionId}.", action.Id);
                return;
            }

            // Update scalar properties
            existingAction.Name = action.Name;
            existingAction.Prefix = action.Prefix;
            existingAction.Instruction = action.Instruction;
            existingAction.Icon = action.Icon;
            existingAction.OutputMode = action.OutputMode;
            existingAction.ExplainChanges = action.ExplainChanges;
            existingAction.RequiresSelection = action.RequiresSelection;
            existingAction.ApplicationContext = action.ApplicationContext;
            existingAction.ActionGroupId = action.ActionGroupId;
            
            existingAction.Placeholders.Clear();
            foreach (var placeholder in action.Placeholders)
            {
                // Ensure new placeholders don't have an ID that could confuse EF Core
                placeholder.Id = 0;
                existingAction.Placeholders.Add(placeholder);
            }
        });
    }

    public Task CreateActionAsync(Action action)
    {
        return ExecuteCommandAsync(async unitOfWork =>
        {
            // If no group is assigned, add it to the default group.
            if (action.ActionGroupId == 0)
            {
                var defaultGroup = await unitOfWork.ActionGroups.GetDefaultGroupAsync() ??
                                   throw new InvalidOperationException("Default 'General' group not found.");
                action.ActionGroupId = defaultGroup.Id;
            }

            var maxSortOrder = await unitOfWork.Actions.GetMaxSortOrderAsync();
            action.SortOrder = maxSortOrder + 1;

            await unitOfWork.Actions.AddAsync(action);
        });
    }

    public Task DeleteActionAsync(int actionId)
    {
        return ExecuteCommandAsync(async unitOfWork =>
        {
            var action = await unitOfWork.Actions.GetByIdAsync(actionId);
            if (action is not null) unitOfWork.Actions.Delete(action);
        });
    }
    
    public Task DeleteActionsAsync(IEnumerable<int> actionIds)
    {
        return ExecuteCommandAsync(async unitOfWork =>
        {
            var actionsToDelete = await unitOfWork.Actions.GetByExpressionAsync(a => actionIds.Contains(a.Id));
            foreach (var action in actionsToDelete)
            {
                unitOfWork.Actions.Delete(action);
            }
        });
    }
    
    public Task MoveActionsToGroupAsync(IEnumerable<int> actionIds, int targetGroupId)
    {
        return ExecuteCommandAsync(async unitOfWork =>
        {
            var actionsToMove = await unitOfWork.Actions.GetByExpressionAsync(a => actionIds.Contains(a.Id));
            var originalGroupIds = actionsToMove.Select(a => a.ActionGroupId).Distinct().ToList();

            foreach (var action in actionsToMove)
            {
                action.ActionGroupId = targetGroupId;
                unitOfWork.Actions.Update(action);
            }
            
            // Re-number sort orders for all affected groups (the target group and all original groups).
            var allAffectedGroupIds = originalGroupIds.Append(targetGroupId).Distinct();
            foreach (var groupId in allAffectedGroupIds)
            {
                var groupActions = (await unitOfWork.Actions.GetByExpressionAsync(a => a.ActionGroupId == groupId))
                    .OrderBy(a => a.SortOrder).ToList();

                for (var i = 0; i < groupActions.Count; i++)
                {
                    groupActions[i].SortOrder = i;
                    unitOfWork.Actions.Update(groupActions[i]);
                }
            }
        });
    }

    public Task UpdateActionOrderAsync(int actionId, int targetGroupId, int newIndex)
    {
        return ExecuteCommandAsync(async unitOfWork =>
        {
            var actionToMove = await unitOfWork.Actions.GetByIdAsync(actionId) ??
                               throw new InvalidOperationException("Action not found.");
            var originalGroupId = actionToMove.ActionGroupId;

            // Get all actions from the target group
            var targetGroupActions = (await unitOfWork.Actions.GetByExpressionAsync(a => a.ActionGroupId == targetGroupId))
                .OrderBy(a => a.SortOrder).ToList();

            // If moving within the same group, remove the item first to get the correct index
            if (originalGroupId == targetGroupId) targetGroupActions.RemoveAll(a => a.Id == actionId);

            // Insert at the new position and update the ActionGroupId
            actionToMove.ActionGroupId = targetGroupId;
            targetGroupActions.Insert(Math.Clamp(newIndex, 0, targetGroupActions.Count), actionToMove);

            // Re-number the sort order for the entire target group
            for (var i = 0; i < targetGroupActions.Count; i++)
            {
                targetGroupActions[i].SortOrder = i;
                unitOfWork.Actions.Update(targetGroupActions[i]);
            }

            // If moved from a different group, re-number the source group as well
            if (originalGroupId != targetGroupId)
            {
                var sourceGroupActions =
                    (await unitOfWork.Actions.GetByExpressionAsync(a => a.ActionGroupId == originalGroupId))
                    .OrderBy(a => a.SortOrder).ToList();
                for (var i = 0; i < sourceGroupActions.Count; i++)
                {
                    sourceGroupActions[i].SortOrder = i;
                    unitOfWork.Actions.Update(sourceGroupActions[i]);
                }
            }
        });
    }
    
    public Task DuplicateActionAsync(int actionId)
    {
        return ExecuteCommandAsync(async unitOfWork =>
        {
            var originalAction = await unitOfWork.Actions.GetByIdWithPlaceholdersAsync(actionId);
            if (originalAction is null)
            {
                logger.LogWarning("Attempted to duplicate a non-existent action with ID {ActionId}.", actionId);
                return;
            }

            var allActionNames = await unitOfWork.Actions.GetAllNamesAsync();
            var newName = GetUniqueActionName($"{originalAction.Name} (Copy)", allActionNames);

            var duplicateAction = new Action
            {
                Name = newName,
                Prefix = originalAction.Prefix,
                Instruction = originalAction.Instruction,
                Icon = originalAction.Icon,
                OutputMode = originalAction.OutputMode,
                ExplainChanges = originalAction.ExplainChanges,
                RequiresSelection = originalAction.RequiresSelection,
                IsFavorite = originalAction.IsFavorite,
                ApplicationContext = [..originalAction.ApplicationContext],
                ActionGroupId = originalAction.ActionGroupId,
                // SortOrder will be handled next
                Placeholders = originalAction.Placeholders.Select(p => new ActionPlaceholder
                {
                    Name = p.Name,
                    Label = p.Label,
                    InputType = p.InputType,
                    OptionsJson = p.OptionsJson,
                    DefaultValue = p.DefaultValue,
                    ValidationJson = p.ValidationJson,
                    DisplayConditionJson = p.DisplayConditionJson
                }).ToList()
            };

            // Insert the duplicate right after the original
            var groupActions = await unitOfWork.Actions.GetByExpressionAsync(a => a.ActionGroupId == originalAction.ActionGroupId);
            var sortedGroupActions = groupActions.OrderBy(a => a.SortOrder).ToList();
            var originalIndex = sortedGroupActions.FindIndex(a => a.Id == originalAction.Id);
            
            duplicateAction.SortOrder = originalAction.SortOrder + 1;
            await unitOfWork.Actions.AddAsync(duplicateAction);
            
            // Re-number the sort order for subsequent items in the group
            for (var i = originalIndex + 1; i < sortedGroupActions.Count; i++)
            {
                sortedGroupActions[i].SortOrder++;
                unitOfWork.Actions.Update(sortedGroupActions[i]);
            }
        });
    }

    /// <summary>
    /// Toggles the favorite status of an action.
    /// </summary>
    /// <param name="actionId">The ID of the action to toggle.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public Task ToggleFavoriteAsync(int actionId)
    {
        return ExecuteCommandAsync(async unitOfWork =>
        {
            var action = await unitOfWork.Actions.GetByIdAsync(actionId);
            if (action is not null)
            {
                action.IsFavorite = !action.IsFavorite;
                unitOfWork.Actions.Update(action);
            }
        });
    }
    
    /// <summary>
    /// Gets a curated list of the most relevant actions based on a priority system:
    /// Favorites > Context-Specific > Recently Used.
    /// </summary>
    /// <param name="appContext">The process name of the currently active application.</param>
    /// <param name="count">The maximum number of actions to return.</param>
    /// <returns>A list of the most relevant actions.</returns>
    public async Task<List<Action>> GetRelevantActionsAsync(string appContext, int count = 5)
    {
        return await ExecuteQueryAsync(async unitOfWork =>
        {
            var addedActionIds = new HashSet<int>();

            // 1. Favorites (Highest Priority)
            var favorites = (await unitOfWork.Actions.GetByExpressionAsync(a => a.IsFavorite))
                .OrderBy(a => a.SortOrder).ToList();
            var relevantActions = favorites.Where(fav => addedActionIds.Add(fav.Id)).ToList();
            if (relevantActions.Count >= count) return relevantActions.Take(count).ToList();

            // 2. Contextual Actions
            var allActions = await unitOfWork.Actions.GetAllAsync();
            var contextualActions = allActions
                .Where(a => a.ApplicationContext.Contains(appContext, StringComparer.OrdinalIgnoreCase))
                .OrderBy(a => a.SortOrder).ToList();
            foreach (var ctx in contextualActions)
            {
                if (addedActionIds.Add(ctx.Id)) 
                    relevantActions.Add(ctx);
                if (relevantActions.Count >= count) 
                    return relevantActions.Take(count).ToList();
            }

            // 3. Recent Actions
            var recentHistory = await unitOfWork.History.GetRecentAsync(20);
            var recentActionNames = recentHistory.Select(h => h.ActionName).Distinct().ToList();

            foreach (var recentAction in recentActionNames.Select(actionName => allActions.FirstOrDefault(a => a.Name.Equals(actionName, StringComparison.OrdinalIgnoreCase))))
            {
                if (recentAction != null && addedActionIds.Add(recentAction.Id)) 
                    relevantActions.Add(recentAction);
                if (relevantActions.Count >= count) 
                    break;
            }

            return relevantActions.Take(count).ToList();
        });
    }

    #endregion

    #region Import/Export

    public async Task ExportActionsToJsonAsync(string filePath)
    {
        var groupsWithActions = await GetActionGroupsWithActionsAsync();

        var exportData = groupsWithActions.ToDictionary(
            g => g.Name,
            g => g.Actions.ToDictionary(
                a => a.Name,
                a => new ActionDto
                {
                    Prefix = a.Prefix,
                    Instruction = a.Instruction,
                    Icon = a.Icon,
                    OutputMode = a.OutputMode,
                    ExplainChanges = a.ExplainChanges,
                    RequiresSelection = a.RequiresSelection,
                    ApplicationContext = a.ApplicationContext,
                    Placeholders = a.Placeholders.Select(p => new ActionPlaceholderDto
                    {
                        Name = p.Name,
                        Label = p.Label,
                        InputType = p.InputType,
                        Options = JsonSerializer.Deserialize<List<string>>(p.OptionsJson) ?? [],
                        DefaultValue = p.DefaultValue
                    })
                }));

        var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
        var json = JsonSerializer.Serialize(exportData, jsonOptions);
        await File.WriteAllTextAsync(filePath, json);
    }
    
    public async Task ExportActionsToJsonAsync(IEnumerable<int> actionIds, string filePath)
    {
        var actionsToExport = await ExecuteQueryAsync(unitOfWork => 
            unitOfWork.Actions.GetByIdsWithDetailsAsync(actionIds));

        if (actionsToExport.Count == 0) return;

        var exportData = actionsToExport
            .GroupBy(a => a.ActionGroup?.Name ?? "General")
            .ToDictionary(
                g => g.Key,
                g => g.ToDictionary(
                    a => a.Name,
                    a => new ActionDto
                    {
                        Prefix = a.Prefix,
                        Instruction = a.Instruction,
                        Icon = a.Icon,
                        OutputMode = a.OutputMode,
                        ExplainChanges = a.ExplainChanges,
                        RequiresSelection = a.RequiresSelection,
                        ApplicationContext = a.ApplicationContext,
                        Placeholders = a.Placeholders.Select(p => new ActionPlaceholderDto
                        {
                            Name = p.Name,
                            Label = p.Label,
                            InputType = p.InputType,
                            Options = JsonSerializer.Deserialize<List<string>>(p.OptionsJson) ?? [],
                            DefaultValue = p.DefaultValue
                        })
                    }));

        var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
        var json = JsonSerializer.Serialize(exportData, jsonOptions);
        await File.WriteAllTextAsync(filePath, json);
    }

    public async Task ImportActionsFromJsonAsync(string filePath)
    {
        await using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        await ImportActionsFromJsonStreamAsync(fileStream);
    }
    
    public async Task ImportActionsFromJsonStreamAsync(Stream jsonStream, ActionConflictResolutionStrategy strategy = ActionConflictResolutionStrategy.Prompt)
    {
        var importedGroups = await JsonSerializer.DeserializeAsync<Dictionary<string, Dictionary<string, ActionDto>>>(jsonStream);

        if (importedGroups is null)
        {
            AppEvents.RequestNotification("Invalid or empty import file.", NotificationType.Error);
            return;
        }

        // Use a single UnitOfWork for the entire import operation for performance and transactional integrity.
        using var scope = scopeFactory.CreateScope();
        await using var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var existingActions = await unitOfWork.Actions.GetAllAsync();
        var existingActionNames = new HashSet<string>(existingActions.Select(a => a.Name), StringComparer.OrdinalIgnoreCase);

        var conflicts = new List<ActionConflict>();
        var nonConflicts = new List<(string GroupName, string ActionName, ActionDto Dto)>();

        // Detect Conflicts
        foreach (var (groupName, actions) in importedGroups)
        foreach (var (actionName, dto) in actions)
        {
            var existingAction = existingActions.FirstOrDefault(a => a.Name.Equals(actionName, StringComparison.OrdinalIgnoreCase));
            if (existingAction is not null)
                conflicts.Add(new ActionConflict(existingAction, dto));
            else
                nonConflicts.Add((groupName, actionName, dto));
        }

        // Resolve Conflicts based on strategy
        List<ActionConflict>? resolvedConflicts = null;
        if (conflicts.Count > 0)
        {
            switch (strategy)
            {
                case ActionConflictResolutionStrategy.Prompt:
                    resolvedConflicts = await AppEvents.RequestConflictResolutionAsync(conflicts);
                    if (resolvedConflicts is null)
                    {
                        AppEvents.RequestNotification("Import canceled by user.", NotificationType.Info);
                        return; // User canceled the dialog
                    }
                    break;
                case ActionConflictResolutionStrategy.Overwrite:
                    resolvedConflicts = conflicts.Select(c => c with { Resolution = ConflictResolutionType.Overwrite }).ToList();
                    break;
                case ActionConflictResolutionStrategy.Skip:
                    resolvedConflicts = conflicts.Select(c => c with { Resolution = ConflictResolutionType.Skip }).ToList();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(strategy), strategy, "Invalid conflict resolution strategy.");
            }
        }

        // Apply changes in a single transaction
        var existingGroups = await unitOfWork.ActionGroups.GetAllAsync();
        var maxActionSortOrder = await unitOfWork.Actions.GetMaxSortOrderAsync();
        var maxGroupSortOrder = await unitOfWork.ActionGroups.GetMaxSortOrderAsync();
        
        // Process resolved conflicts
        if (resolvedConflicts is not null)
        {
            foreach (var conflict in resolvedConflicts)
            {
                switch (conflict.Resolution)
                {
                    case ConflictResolutionType.Skip:
                        continue; // Do nothing
                    
                    case ConflictResolutionType.Overwrite:
                        var actionToUpdate = await unitOfWork.Actions.GetByIdWithPlaceholdersAsync(conflict.ExistingAction.Id) ?? throw new InvalidOperationException("Action to overwrite not found during import.");
    
                        actionToUpdate.Prefix = conflict.ImportedActionDto.Prefix;
                        actionToUpdate.Instruction = conflict.ImportedActionDto.Instruction;
                        actionToUpdate.Icon = conflict.ImportedActionDto.Icon;
                        actionToUpdate.OutputMode = conflict.ImportedActionDto.OutputMode;
                        actionToUpdate.ExplainChanges = conflict.ImportedActionDto.ExplainChanges;
                        actionToUpdate.RequiresSelection = conflict.ImportedActionDto.RequiresSelection;
                        actionToUpdate.ApplicationContext = conflict.ImportedActionDto.ApplicationContext.ToList();

                        actionToUpdate.Placeholders.Clear();
                        foreach (var p in conflict.ImportedActionDto.Placeholders)
                        {
                            actionToUpdate.Placeholders.Add(new ActionPlaceholder
                            {
                                Name = p.Name,
                                Label = p.Label,
                                InputType = p.InputType,
                                OptionsJson = JsonSerializer.Serialize(p.Options),
                                DefaultValue = p.DefaultValue,
                                ValidationJson = p.ValidationJson,
                                DisplayConditionJson = p.DisplayConditionJson
                            });
                        }
                        
                        unitOfWork.Actions.Update(actionToUpdate);
                        break;
                        
                    case ConflictResolutionType.Rename:
                        var newName = GetUniqueActionName(conflict.ExistingAction.Name, existingActionNames);
                        existingActionNames.Add(newName); // Add to the set to handle multiple renames of the same original name
                        var groupName = importedGroups.FirstOrDefault(g => g.Value.ContainsKey(conflict.ExistingAction.Name)).Key;
                        await CreateNewActionAsync(unitOfWork, newName, groupName, conflict.ImportedActionDto, existingGroups, ++maxActionSortOrder, ++maxGroupSortOrder);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(conflict.Resolution), conflict.Resolution, "Invalid conflict resolution type.");
                }
            }
        }
        
        // Process non-conflicting actions
        foreach (var (groupName, actionName, dto) in nonConflicts)
        {
            await CreateNewActionAsync(unitOfWork, actionName, groupName, dto, existingGroups, ++maxActionSortOrder, ++maxGroupSortOrder);
        }

        await unitOfWork.SaveChangesAsync();
        
        if (strategy == ActionConflictResolutionStrategy.Prompt) 
            AppEvents.RequestNotification("Actions imported successfully.", NotificationType.Success);
    }

    #endregion
    
    #region Private Helpers

    private static string GetUniqueActionName(string originalName, ICollection<string> existingNames)
    {
        var newName = originalName;
        var suffix = 1;
        while (existingNames.Contains(newName, StringComparer.OrdinalIgnoreCase))
        {
            newName = $"{originalName} ({suffix++})";
        }
        return newName;
    }

    private static async Task CreateNewActionAsync(IUnitOfWork unitOfWork, string actionName, string? groupName, ActionDto dto, ICollection<ActionGroup> existingGroups, int actionSortOrder, int groupSortOrder)
    {
        if (string.IsNullOrWhiteSpace(groupName))
        {
            groupName = "General"; // Fallback to default group if group name is missing
        }

        var group = existingGroups.FirstOrDefault(g => g.Name.Equals(groupName, StringComparison.OrdinalIgnoreCase));
        if (group is null)
        {
            group = new ActionGroup { Name = groupName, SortOrder = groupSortOrder };
            await unitOfWork.ActionGroups.AddAsync(group);
            existingGroups.Add(group);
        }

        var newAction = new Action
        {
            Name = actionName,
            Prefix = dto.Prefix,
            Instruction = dto.Instruction,
            Icon = dto.Icon,
            OutputMode = dto.OutputMode,
            ExplainChanges = dto.ExplainChanges,
            RequiresSelection = dto.RequiresSelection,
            ApplicationContext = dto.ApplicationContext.ToList(),
            ActionGroupId = group.Id,
            ActionGroup = group,
            SortOrder = actionSortOrder,
            Placeholders = dto.Placeholders.Select(p => new ActionPlaceholder
            {
                Name = p.Name,
                Label = p.Label,
                InputType = p.InputType,
                OptionsJson = JsonSerializer.Serialize(p.Options),
                DefaultValue = p.DefaultValue
            }).ToList()
        };

        await unitOfWork.Actions.AddAsync(newAction);
    }
    
    /// <summary>
    /// Creates a UoW scope, executes a command, and saves changes.
    /// </summary>
    private async Task ExecuteCommandAsync(Func<IUnitOfWork, Task> command)
    {
        using var scope = scopeFactory.CreateScope();
        await using var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        await command(unitOfWork);
        await unitOfWork.SaveChangesAsync();
    }

    /// <summary>
    /// Creates a UoW scope and executes a query.
    /// </summary>
    private async Task<T> ExecuteQueryAsync<T>(Func<IUnitOfWork, Task<T>> query)
    {
        using var scope = scopeFactory.CreateScope();
        await using var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        return await query(unitOfWork);
    }

    #endregion
}