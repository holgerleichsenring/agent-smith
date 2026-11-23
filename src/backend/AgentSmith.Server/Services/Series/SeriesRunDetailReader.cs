using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Series;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.Tickets;

namespace AgentSmith.Server.Services.Series;

/// <summary>2026-11-23-31dcb: one series run as its page reads it — tickets in depth order with their pull requests and
/// CI per repo, progress and merge order — from the same set-based reads as the list; null when there is no such series
/// or its project is no longer configured. 2026-11-23-31dcd: and the bar's facts — the repos it touches, what it cost,
/// and its conversation's thread, so the page can carry that conversation. 2026-11-23-31dcf: and what each requires edge
/// between its tickets means for the branches (<see cref="SeriesRunEdges"/>). One read after another on the request's
/// unit of work.</summary>
public sealed class SeriesRunDetailReader(
    ISeriesRunStore store, TicketProjects projects, SeriesRunProjectReads reads, SeriesRunTickets tickets, SeriesRunNames names,
    SeriesRunCosts costs, SpecDialogSessionRepository sessions, SeriesRunEdges edges)
{
    public async Task<SeriesRunDetailView?> ReadAsync(string id, CancellationToken ct)
    {
        if (await store.GetAsync(id, ct) is not { } record || projects.Of(record.Project) is not { } project) return null;
        var facts = await reads.ReadAsync(project, record.Tickets, ct);
        var shape = tickets.Of(record, facts);
        var name = (await names.OfAsync([record], ct))[record.Id];
        var spent = await costs.OfAsync(record.Project, [.. record.Tickets.Select(t => t.TicketId)], ct);
        var session = await sessions.GetBySessionIdAsync(record.Conversation, ct);
        return new SeriesRunDetailView(record.Id, name, record.Project, record.State, record.Conversation, record.StartedAt,
            record.FinishedAt, shape.Counts, SeriesRunQuestion.Of(record), shape.Tickets, shape.MergeOrder)
        {
            Repos = Repos(project, shape.Tickets), CostUsd = spent + record.MasterCostUsd,
            ThreadId = session?.ThreadId, ConversationOpen = session?.IsOpen == true,
            Edges = await edges.OfAsync(facts, [.. record.Tickets.Select(t => t.TicketId)], ct),
        };
    }

    // The repos the series' tickets have pull requests in, in the project's repo order.
    private static IReadOnlyList<string> Repos(ResolvedProject project, IReadOnlyList<SeriesRunTicketView> tickets)
    {
        var touched = tickets.SelectMany(t => t.Repos).ToHashSet(StringComparer.Ordinal);
        var configured = project.Repos.Select(r => r.Name).ToList();
        return [.. configured.Where(touched.Contains), .. touched.Except(configured).Order(StringComparer.Ordinal)];
    }
}
