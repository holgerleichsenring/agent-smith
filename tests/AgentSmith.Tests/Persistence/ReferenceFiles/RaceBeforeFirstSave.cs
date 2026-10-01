using Microsoft.EntityFrameworkCore.Diagnostics;

namespace AgentSmith.Tests.Persistence.ReferenceFiles;

/// <summary>
/// 2026-10-01-283da: runs another actor's work once, between a copier's read and its first
/// write — the interleaving a lease handover produces, made deterministic.
/// </summary>
internal sealed class RaceBeforeFirstSave(Func<Task> rival) : SaveChangesInterceptor
{
    private bool _fired;

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (_fired) return result;
        _fired = true;
        await rival();
        return result;
    }
}
