using Microsoft.Extensions.DependencyInjection;
using ProseFlow.Core.Interfaces;
using ProseFlow.Core.Models;

namespace ProseFlow.Application.Services;

/// <summary>
/// Manages creating and retrieving history entries.
/// </summary>
public class HistoryService(IServiceScopeFactory scopeFactory)
{
    /// <summary>
    /// Adds a new entry to the history.
    /// </summary>
    public Task AddHistoryEntryAsync(string actionName, string instruction, string providerUsed, string modelUsed, string input, string output,
        long promptTokens, long completionTokens, double latencyMs, double inferenceSpeed)
    {
        return ExecuteCommandAsync(async unitOfWork =>
        {
            var entry = new HistoryEntry
            {
                Timestamp = DateTime.UtcNow,
                ActionName = actionName,
                Instruction = instruction,
                ProviderUsed = providerUsed,
                ModelUsed = modelUsed,
                InputText = input,
                OutputText = output,
                PromptTokens = promptTokens,
                CompletionTokens = completionTokens,
                LatencyMs = latencyMs,
                TokensPerSecond = inferenceSpeed
            };

            await unitOfWork.History.AddAsync(entry);
        });
    }

    public Task<List<HistoryEntry>> GetRecentHistoryAsync(int count = 10)
    {
        return ExecuteQueryAsync(unitOfWork => unitOfWork.History.GetRecentAsync(count));
    }

    public Task<List<HistoryEntry>> GetHistoryByDateRangeAsync(DateTime startDate, DateTime endDate)
    {
        return ExecuteQueryAsync(unitOfWork => unitOfWork.History.GetByDateRangeAsync(startDate, endDate));
    }

    /// <summary>
    /// Retrieves all history entries, ordered by the most recent, with optional filtering and searching.
    /// </summary>
    /// <param name="searchTerm">The text to search for within the history entries.</param>
    /// <param name="filterType">The property to search within (e.g., "Action Name", "Input"). Defaults to all relevant fields.</param>
    public Task<List<HistoryEntry>> GetHistoryAsync(string? searchTerm = null, string? filterType = null)
    {
        return ExecuteQueryAsync(unitOfWork => unitOfWork.History.GetAllOrderedByTimestampAsync(searchTerm, filterType));
    }
    
    /// <summary>
    /// Deletes a history entry by ID.
    /// </summary>
    /// <param name="entry">The history entry to delete.</param>
    /// <returns>A task representing the asynchronous deletion operation.</returns>
    /// <remarks>
    /// If the given history entry does not exist, this method does nothing.
    /// </remarks>
    public Task DeleteHistoryEntryAsync(HistoryEntry entry)
    {
        return ExecuteCommandAsync(async unitOfWork =>
        {
            var historyEntry = await unitOfWork.History.GetByIdAsync(entry.Id);
            if (historyEntry is not null) unitOfWork.History.Delete(historyEntry);
        });
    }

    /// <summary>
    /// Deletes all entries from the history.
    /// </summary>
    public Task ClearHistoryAsync()
    {
        return ExecuteCommandAsync(unitOfWork => unitOfWork.History.ClearAllAsync());
    }

    #region Private Helpers

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