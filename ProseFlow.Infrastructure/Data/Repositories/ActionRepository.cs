using Microsoft.EntityFrameworkCore;
using ProseFlow.Core.Interfaces.Repositories;
using Action = ProseFlow.Core.Models.Action;

namespace ProseFlow.Infrastructure.Data.Repositories;

public class ActionRepository(AppDbContext context) : Repository<Action>(context), IActionRepository
{
    /// <inheritdoc />
    public async Task<List<Action>> GetAllOrderedAsync()
    {
        return await Context.Actions
            .Include(a => a.ActionGroup)
            .Include(a => a.Placeholders)
            .OrderBy(a => a.SortOrder)
            .ToListAsync();
    }
    
    /// <inheritdoc />
    public async Task<List<Action>> GetByIdsWithDetailsAsync(IEnumerable<int> ids)
    {
        return await Context.Actions
            .Include(a => a.ActionGroup)
            .Include(a => a.Placeholders)
            .Where(a => ids.Contains(a.Id))
            .ToListAsync();
    }
    
    /// <inheritdoc />
    public async Task<Action?> GetByIdWithPlaceholdersAsync(int id)
    {
        return await Context.Actions
            .Include(a => a.Placeholders)
            .FirstOrDefaultAsync(a => a.Id == id);
    }
    
    /// <inheritdoc />
    public async Task<int> GetMaxSortOrderAsync()
    {
        return await Context.Actions.AnyAsync()
            ? await Context.Actions.MaxAsync(a => a.SortOrder)
            : 0;
    }

    /// <inheritdoc />
    public async Task<List<string>> GetAllNamesAsync()
    {
        return await Context.Actions.Select(a => a.Name).ToListAsync();
    }
    
    /// <inheritdoc />
    public async Task UpdateOrderAsync(List<Action> orderedActions)
    {
        for (var i = 0; i < orderedActions.Count; i++)
        {
            var actionToUpdate = await Context.Actions.FindAsync(orderedActions[i].Id);
            if (actionToUpdate != null) actionToUpdate.SortOrder = i;
        }
    }
}