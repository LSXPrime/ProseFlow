using Microsoft.EntityFrameworkCore;
using ProseFlow.Core.Interfaces.Repositories;
using ProseFlow.Core.Models;

namespace ProseFlow.Infrastructure.Data.Repositories;

public class ActionGroupRepository(AppDbContext context) : Repository<ActionGroup>(context), IActionGroupRepository
{
    public async Task<List<ActionGroup>> GetAllOrderedAsync()
    {
        return await Context.ActionGroups.OrderBy(g => g.SortOrder).ToListAsync();
    }

    public async Task<List<ActionGroup>> GetAllOrderedWithActionsAsync()
    {
        return await Context.ActionGroups
            .Include(g => g.Actions.OrderBy(a => a.SortOrder))
            .ThenInclude(a => a.Placeholders)
            .OrderBy(g => g.SortOrder)
            .ToListAsync();
    }

    public async Task UpdateOrderAsync(List<ActionGroup> orderedGroups)
    {
        for (var i = 0; i < orderedGroups.Count; i++)
        {
            var groupToUpdate = await Context.ActionGroups.FindAsync(orderedGroups[i].Id);
            if (groupToUpdate != null) groupToUpdate.SortOrder = i;
        }
    }
    
    public async Task<int> GetMaxSortOrderAsync()
    {
        return await Context.ActionGroups.AnyAsync()
            ? await Context.ActionGroups.MaxAsync(a => a.SortOrder)
            : 0;
    }

    public async Task<ActionGroup?> GetDefaultGroupAsync()
    {
        // The default group is seeded with ID = 1.
        return await Context.ActionGroups.FindAsync(1);
    }
}