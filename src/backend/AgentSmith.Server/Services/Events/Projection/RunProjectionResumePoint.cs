using AgentSmith.Contracts.Events;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Server.Services.Events.Projection;

/// <summary>
/// 2026-10-26-0f19c: the stream entry after which a run's events may be missing from the store. Trail
/// rows are flushed in stream order and only a sandbox result writes its row out of that order
/// (2026-10-26-0f19a), so the highest other position bounds what was persisted; every event above it is
/// read again. A run with no trail is read from the start. A run whose trail holds only counted
/// positions predates positions from the stream and has no entry to resume after: null.
/// </summary>
public sealed class RunProjectionResumePoint(IServiceScopeFactory scopes)
{
    public const string FromStart = "0-0";

    public async Task<string?> AfterAsync(string runId, CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var rows = scope.ServiceProvider.GetRequiredService<IUnitOfWork>()
            .Set<AgentSmith.Infrastructure.Persistence.Entities.RunEvent>().AsNoTracking()
            .Where(e => e.RunId == runId);
        var ordered = await rows
            .Where(e => e.Seq >= StreamTrailPosition.Floor && e.Type != nameof(EventType.SandboxResult))
            .MaxAsync(e => (long?)e.Seq, ct);
        if (ordered is { } position) return StreamTrailPosition.ToEntryId(position);
        return await rows.AnyAsync(e => e.Seq < StreamTrailPosition.Floor, ct) ? null : FromStart;
    }
}
