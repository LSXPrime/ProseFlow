using Action = ProseFlow.Core.Models.Action;

namespace ProseFlow.Core.Interfaces.Repositories;

public interface IActionRepository : IRepository<Action>
{
    /// <summary>
    /// Gets a list of actions ordered by their SortOrder.
    /// </summary>
    /// <returns>A list of Action entities ordered by SortOrder.</returns>
    Task<List<Action>> GetAllOrderedAsync();
    /// <summary>
    /// Gets a list of actions by their IDs, eagerly loading their ActionGroup and Placeholders collections.
    /// </summary>
    /// <param name="ids">The IDs of the actions to retrieve.</param>
    /// <returns>A list of fully loaded Action entities.</returns>
    Task<List<Action>> GetByIdsWithDetailsAsync(IEnumerable<int> ids);
    
    /// <summary>
    /// Gets an action by its ID, eagerly loading its Placeholders collection.
    /// </summary>
    Task<Action?> GetByIdWithPlaceholdersAsync(int id);
    
    Task<int> GetMaxSortOrderAsync();
    Task<List<string>> GetAllNamesAsync();
    Task UpdateOrderAsync(List<Action> orderedActions);
}