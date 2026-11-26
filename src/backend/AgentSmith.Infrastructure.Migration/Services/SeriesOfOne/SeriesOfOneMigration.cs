using System.Text.Json;
using System.Text.Json.Nodes;
using AgentSmith.Infrastructure.Core.Services.Configuration.Studio;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Series;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Migration.Services.SeriesOfOne;

/// <summary>
/// 2026-11-25-96aea: the standalone tickets in flight (<see cref="SeriesOfOneCandidates"/>) each get a series of one, so their
/// series master leads them now the server's CI loop and automatic starts are gone. Its id is derived from project and ticket,
/// so a second migrate — or a second init container racing this one — opens no second; its review is the stored project's (none
/// stays none, anything else is each: one ticket, one flow); a ticket whose start waits gets a wake its master decides on.
/// Safe under replicas: 1 with strategy: Recreate, where the old server is gone before this runs.
/// </summary>
public sealed class SeriesOfOneMigration(ILogger<SeriesOfOneMigration> logger) : IDataMigration
{
    public string Id => "2026-11-25-96aea-series-of-one";

    public string? ShippedWith => null;

    public async Task<bool> RunAsync(AgentSmithDbContext db, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(db);
        var reviews = Reviews(new ContextConfigDocumentStore(db));
        var opened = 0;
        foreach (var ticket in await SeriesOfOneCandidates.ReadAsync(db, ct))
            opened += await OpenAsync(db, Record(ticket, reviews), ct) ? 1 : 0;
        logger.LogInformation("Opened {Count} series of one over the standalone tickets in flight", opened);
        return true;
    }

    /// <summary>The series id of a ticket's migrated series of one: the same for the same ticket on every run.</summary>
    public static string IdOf(string project, string ticketId) => Application.Services.Series.SeriesRunIds.OneOf(project, ticketId, "migrated");

    private static SeriesRunRecord Record(SeriesOfOneCandidate ticket, IReadOnlyDictionary<string, ReviewMode> reviews)
    {
        var now = DateTimeOffset.UtcNow;
        var key = Application.Services.Claim.RequiresUnmetGate.Named(ticket.TicketId);
        var wake = new SeriesWake(SeriesWakeKinds.Prerequisite, "it was filed waiting on its prerequisites; start_ticket starts it once "
            + "all it requires is met") { At = now, TicketId = ticket.TicketId, Once = $"migrated-start:{ticket.TicketId}" };
        return new SeriesRunRecord(IdOf(ticket.Project, ticket.TicketId), string.Empty, ticket.Project, Contracts.Runs.AttemptChannels.SeriesActor,
            true, SeriesRunStates.Running, [new SeriesTicket(ticket.TicketId, key, ticket.Title ?? key, false, now)], now)
        {
            Review = reviews.GetValueOrDefault(ticket.Project) == ReviewMode.None ? ReviewMode.None : ReviewMode.Each,
            Wakes = ticket.AwaitsStart ? [wake] : [], Raised = ticket.AwaitsStart ? [wake.Once!] : [],
        };
    }

    // One row per series, shaped as SeriesRunRepository writes it; a row another migrate inserted first is no failure.
    private async Task<bool> OpenAsync(AgentSmithDbContext db, SeriesRunRecord record, CancellationToken ct)
    {
        if (await db.Set<SeriesRun>().AnyAsync(s => s.SeriesRunId == record.Id, ct)) return false;
        db.Add(new SeriesRun
        {
            SeriesRunId = record.Id, SessionId = record.Conversation, Project = record.Project, State = record.State,
            ContentJson = JsonSerializer.Serialize(record), Version = record.Version,
        });
        try { await db.SaveChangesAsync(ct); return true; }
        catch (DbUpdateException ex)
        {
            logger.LogInformation(ex, "Series of one {Series} was opened by another migrate", record.Id);
            db.ChangeTracker.Clear();
            return false;
        }
    }

    // Each stored project's review; a project without one runs under the default (end), which is each for one ticket.
    private static IReadOnlyDictionary<string, ReviewMode> Reviews(IConfigDocumentStore documents) =>
        documents.LoadAll().Where(r => r.Type == ConfigDocTypes.Project)
            .Select(r => (r.Id, Review: JsonNode.Parse(r.Doc) is JsonObject doc
                && doc.FirstOrDefault(p => string.Equals(p.Key, "Review", StringComparison.OrdinalIgnoreCase)).Value?.ToString() is { } text
                && string.Equals(text.Trim(), "none", StringComparison.OrdinalIgnoreCase) ? ReviewMode.None : ReviewMode.End))
            .GroupBy(p => p.Id, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First().Review, StringComparer.Ordinal);
}
