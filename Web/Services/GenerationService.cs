using Microsoft.EntityFrameworkCore;
using Narratum.Core;
using Narratum.Persistence;

namespace Narratum.Web.Services;

/// <summary>
/// STUB SERVICE - Will be implemented properly later.
/// For now, just makes the Web project compile.
/// </summary>
public class GenerationService
{
    private readonly NarrativumDbContext _dbContext;

    public GenerationService(NarrativumDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<Result<string>> CreateStoryAsync(
        string slotName,
        string worldName,
        string genreStyle,
        List<string> characterNames,
        CancellationToken ct = default)
    {
        // STUB - Not implemented yet
        return Task.FromResult(Result<string>.Ok(slotName));
    }

    public Task<Result<PageInfo>> GenerateNextPageAsync(
        string slotName,
        string intentDescription,
        CancellationToken ct = default)
    {
        // STUB - Not implemented yet
        return Task.FromResult(Result<PageInfo>.Fail("Génération non implémentée"));
    }

    public async Task<Result<PageInfo>> LoadPageAsync(
        string slotName,
        int pageIndex,
        CancellationToken ct = default)
    {
        var snapshot = await _dbContext.PageSnapshots
            .FirstOrDefaultAsync(p => p.SlotName == slotName && p.PageIndex == pageIndex, ct);

        if (snapshot == null)
            return Result<PageInfo>.Fail($"Page {pageIndex} introuvable");

        return Result<PageInfo>.Ok(new PageInfo(
            snapshot.PageIndex,
            snapshot.NarrativeText ?? "",
            snapshot.GeneratedAt));
    }

    public async Task<List<int>> GetPageHistoryAsync(
        string slotName,
        CancellationToken ct = default)
    {
        return await _dbContext.PageSnapshots
            .Where(p => p.SlotName == slotName)
            .OrderBy(p => p.PageIndex)
            .Select(p => p.PageIndex)
            .ToListAsync(ct);
    }
}

public record PageInfo(
    int PageIndex,
    string NarrativeText,
    DateTime GeneratedAt);
