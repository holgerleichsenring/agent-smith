using AgentSmith.Contracts.Models.Access;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Repositories;

/// <summary>
/// 2026-08-26-7a51: the observed callers, read and written as whole rows. Every call here
/// is off the request path — the authorization handler buffers in memory and a hosted
/// service flushes — so a window's worth of callers is one transaction rather than one
/// write per request.
/// </summary>
public sealed class ObservedCallerRepository(IUnitOfWork unitOfWork)
{
    public async Task UpsertAsync(IReadOnlyList<ObservedCaller> callers, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(callers);
        var subjects = callers.Select(c => c.Subject).ToList();
        var existing = await unitOfWork.Set<ObservedCallerEntity>()
            .Where(e => subjects.Contains(e.Subject)).ToDictionaryAsync(e => e.Subject, ct);
        foreach (var caller in callers) Apply(caller, existing.GetValueOrDefault(caller.Subject));
        await unitOfWork.SaveChangesAsync(ct);
    }

    // Ordered and filtered on the client throughout: SQLite refuses a DateTimeOffset in
    // both an ORDER BY and a comparison, and one row per caller is a small enough table
    // that one portable path beats four provider-specific ones.
    public async Task<IReadOnlyList<ObservedCaller>> AllAsync(CancellationToken ct) =>
        [.. (await unitOfWork.Set<ObservedCallerEntity>().AsNoTracking().ToListAsync(ct))
            .OrderByDescending(e => e.LastSeen).Select(Map)];

    public async Task<bool> RemoveAsync(string subject, CancellationToken ct)
    {
        var row = await unitOfWork.Set<ObservedCallerEntity>()
            .FirstOrDefaultAsync(e => e.Subject == subject, ct);
        if (row is null) return false;
        unitOfWork.Remove(row);
        await unitOfWork.SaveChangesAsync(ct);
        return true;
    }

    public async Task<int> RemoveSeenBeforeAsync(DateTimeOffset cut, CancellationToken ct)
    {
        var stale = (await unitOfWork.Set<ObservedCallerEntity>().ToListAsync(ct))
            .Where(e => e.LastSeen < cut).ToList();
        foreach (var row in stale) unitOfWork.Remove(row);
        if (stale.Count > 0) await unitOfWork.SaveChangesAsync(ct);
        return stale.Count;
    }

    private void Apply(ObservedCaller caller, ObservedCallerEntity? row)
    {
        if (row is null)
        {
            unitOfWork.Add(Entity(caller));
            return;
        }
        // First seen is the one field a later observation never moves.
        row.NameClaim = caller.NameClaim;
        row.NameValue = caller.NameValue;
        row.RoleValues = Join(caller.RoleValues);
        row.GroupValues = Join(caller.GroupValues);
        row.GroupsOmitted = caller.GroupsOmitted;
        row.LastSeen = caller.LastSeen;
    }

    private static ObservedCallerEntity Entity(ObservedCaller caller) => new()
    {
        Subject = caller.Subject,
        NameClaim = caller.NameClaim,
        NameValue = caller.NameValue,
        RoleValues = Join(caller.RoleValues),
        GroupValues = Join(caller.GroupValues),
        GroupsOmitted = caller.GroupsOmitted,
        FirstSeen = caller.FirstSeen,
        LastSeen = caller.LastSeen,
    };

    private static ObservedCaller Map(ObservedCallerEntity row) => new(
        row.Subject, row.NameClaim, row.NameValue,
        Split(row.RoleValues), Split(row.GroupValues), row.GroupsOmitted, row.FirstSeen, row.LastSeen);

    private static string Join(IReadOnlyList<string> values) => string.Join('\n', values);

    private static IReadOnlyList<string> Split(string value) =>
        value.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
