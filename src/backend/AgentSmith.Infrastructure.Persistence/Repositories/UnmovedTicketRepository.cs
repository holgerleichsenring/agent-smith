using AgentSmith.Contracts.Models;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Repositories;

/// <summary>
/// 2026-09-18-c1a7: data access for the unmoved-ticket record over a scoped unit of work.
/// It also owns the RELEASE, because the observable the release reads lives here: the config
/// document's own version. A record stamped under configuration that has since changed no
/// longer stands — correcting the status on the tracker or on the project is the fix, and
/// bumping either document's version is what that fix looks like from this side.
/// </summary>
public sealed class UnmovedTicketRepository(IUnitOfWork unitOfWork)
{
    // Internal, and pinned to the config taxonomy by UnmovedTicketRecordTests: a renamed doc
    // type would otherwise make every version read zero and the stop permanent.
    internal const string TrackerDocType = "tracker";
    internal const string ProjectDocType = "project";

    public async Task RecordAsync(UnmovedTicketFact fact, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(fact);
        var row = await RowAsync(fact.Project, fact.TicketId, ct);
        if (row is null)
        {
            row = new UnmovedTicket { Project = fact.Project, TicketId = fact.TicketId };
            unitOfWork.Add(row);
        }
        row.Tracker = fact.Tracker;
        row.ConfiguredStatus = fact.ConfiguredStatus;
        row.Outcome = (int)fact.Outcome;
        row.TrackerConfigVersion = await VersionAsync(TrackerDocType, fact.Tracker, ct);
        row.ProjectConfigVersion = await VersionAsync(ProjectDocType, fact.Project, ct);
        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task<UnmovedTicketFact?> FindStandingAsync(
        string project, string ticketId, string tracker, CancellationToken ct)
    {
        var row = await RowAsync(project, ticketId, ct);
        if (row is null) return null;
        // A record the configuration has moved past is not merely ignored, it is DROPPED: left
        // lying it would stand again the day an export/re-import rewound the version it names.
        if (!await StillUnderTheSameConfigurationAsync(row, tracker, ct)
            || (TicketFinalizeOutcome)row.Outcome == TicketFinalizeOutcome.Moved)
        {
            unitOfWork.Remove(row);
            await unitOfWork.SaveChangesAsync(ct);
            return null;
        }
        return new UnmovedTicketFact(
            row.Project, row.TicketId, row.Tracker, row.ConfiguredStatus,
            (TicketFinalizeOutcome)row.Outcome);
    }

    public async Task ClearAsync(string project, string ticketId, CancellationToken ct)
    {
        var row = await RowAsync(project, ticketId, ct);
        if (row is null) return;
        unitOfWork.Remove(row);
        await unitOfWork.SaveChangesAsync(ct);
    }

    // A project pointed at a different tracker is itself a configuration change, so the
    // name is compared as well as the two versions.
    private async Task<bool> StillUnderTheSameConfigurationAsync(
        UnmovedTicket row, string tracker, CancellationToken ct) =>
        string.Equals(row.Tracker, tracker, StringComparison.Ordinal)
        && await VersionAsync(TrackerDocType, row.Tracker, ct) == row.TrackerConfigVersion
        && await VersionAsync(ProjectDocType, row.Project, ct) == row.ProjectConfigVersion;

    private Task<UnmovedTicket?> RowAsync(string project, string ticketId, CancellationToken ct) =>
        unitOfWork.Set<UnmovedTicket>()
            .FirstOrDefaultAsync(t => t.Project == project && t.TicketId == ticketId, ct);

    // 0 when the store holds no such document — a file-configured server never bumps it,
    // and the record then stands until the operator's retry clears it.
    private async Task<int> VersionAsync(string type, string id, CancellationToken ct) =>
        await unitOfWork.Set<ConfigEntity>().AsNoTracking()
            .Where(c => c.EntityType == type && c.EntityId == id)
            .Select(c => (int?)c.Version)
            .FirstOrDefaultAsync(ct) ?? 0;
}
