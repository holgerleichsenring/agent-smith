using System.Text.Json;
using AgentSmith.Contracts.Runs;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Server.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Server.Services.Tickets;

/// <summary>
/// 2026-10-13-9a59d: the RunAttempt rows of a set of runs, in one query, as the ticket views read
/// them. A run without a row (a queued reservation, a run from before the record) is absent.
/// </summary>
public sealed class TicketAttemptRecords(IServiceScopeFactory scopes)
{
    public async Task<IReadOnlyDictionary<string, TicketAttemptRecord>> ForAsync(
        IReadOnlyList<string> runIds, CancellationToken ct)
    {
        if (runIds.Count == 0) return new Dictionary<string, TicketAttemptRecord>();
        using var scope = scopes.CreateScope();
        var rows = await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().Set<RunAttempt>().AsNoTracking()
            .Where(a => runIds.Contains(a.RunId))
            .ToListAsync(ct);
        return rows.ToDictionary(a => a.RunId, Record, StringComparer.Ordinal);
    }

    private static TicketAttemptRecord Record(RunAttempt row) =>
        new(Cause(row), Revision(row), row.TrackerState, row.WebUrl);

    private static AttemptCause? Cause(RunAttempt row) =>
        row.CauseChannel is not { } channel ? null
            : new AttemptCause(channel, row.CauseActor, row.CauseAt,
                row.SourceKind is { } kind ? new AttemptSource(kind, row.SourceUrl, row.SourceId, row.SourceDetail) : null);

    private static TicketAttemptRevisionView? Revision(RunAttempt row) =>
        row.RevisionNumber is not { } number ? null
            : new TicketAttemptRevisionView(number, row.RevisionCause, row.SeriesId, CarriedIds(row.CarriedSpecIdsJson), row.ApprovalAt);

    private static IReadOnlyList<string> CarriedIds(string? json) =>
        string.IsNullOrEmpty(json) ? [] : JsonSerializer.Deserialize<List<string>>(json) ?? [];
}
