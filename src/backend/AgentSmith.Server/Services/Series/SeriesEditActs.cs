using AgentSmith.Application.Services.Filing;
using AgentSmith.Contracts.Series;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Server.Services.Tickets;

namespace AgentSmith.Server.Services.Series;

/// <summary>
/// 2026-11-25-9992f: the series master's changes to its series' cut. An added ticket is filed through the 'file' run
/// (<see cref="IFilingRun"/>) seeded with the series id, so it joins the series — and is written from the series branch —
/// before anything could start it; it is filed unstarted and its edges are stored next, so it starts once they
/// stand. Edges between members are added and removed through <see cref="SeriesRequirementLinks"/>. An edge that would let
/// a ticket wait on itself is refused before anything is filed or written.
/// </summary>
public sealed class SeriesEditActs(TicketProjects projects, IFilingRun filing, SeriesRequirementLinks links) : ISeriesEditActs
{
    public async Task<SeriesCutAnswer> AddTicketAsync(SeriesRunRecord series, string title, string body, IReadOnlyList<string> requires,
        IReadOnlyList<string> requiredBy, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(series);
        if (projects.Of(series.Project) is not { } project) return new($"Project '{series.Project}' is not configured.", false);
        if (AddCycle(series, await links.GraphAsync(project, cancellationToken), requires, requiredBy) is { } refused) return new(refused, false);
        var report = await filing.RunRequestAsync(project.Name, Request(series, title, body), series.Id);
        if (report.Filed.FirstOrDefault(f => !string.IsNullOrEmpty(f.TicketId)) is not { } filed)
            return new($"Nothing was filed: {report.Error ?? "the filing run filed no ticket"}.", false);
        var id = filed.TicketId!;
        var key = filed.Key ?? id;
        try { await LinkAddedAsync(project, series, id, requires, requiredBy, cancellationToken); }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new($"Filed {key} into the series, unstarted, but its requirement edges were not stored: "
                + $"{ex.Message.Split('\n', 2)[0].Trim()}. link_tickets stores them.", true, id, key);
        }
        return new($"Filed {key} into the series, unstarted{Edges(series, requires, requiredBy)}; it starts once what it requires "
            + "has landed, or by start_ticket.", true, id, key);
    }

    private async Task LinkAddedAsync(AgentSmith.Contracts.Models.Configuration.ResolvedProject project, SeriesRunRecord series, string id,
        IReadOnlyList<string> requires, IReadOnlyList<string> requiredBy, CancellationToken ct)
    {
        await links.AddAsync(project, series.Conversation, id, requires, ct);
        foreach (var dependent in requiredBy.Distinct(StringComparer.Ordinal))
            await links.AddAsync(project, series.Conversation, dependent, [id], ct);
    }

    public async Task<SeriesCutAnswer> LinkAsync(SeriesRunRecord series, string ticketId, IReadOnlyList<string> addRequires,
        IReadOnlyList<string> removeRequires, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(series);
        if (projects.Of(series.Project) is not { } project) return new($"Project '{series.Project}' is not configured.", false);
        var graph = await links.GraphAsync(project, cancellationToken);
        foreach (var required in removeRequires) graph.Remove(ticketId, required);
        if (LinkCycle(series, graph, ticketId, addRequires) is { } refused) return new(refused, false);
        var said = new List<string>();
        foreach (var required in removeRequires.Distinct(StringComparer.Ordinal))
            said.Add(await links.RemoveAsync(project, ticketId, required, cancellationToken)
                ? $"{Name(series, ticketId)} no longer requires {Name(series, required)}."
                : $"{Name(series, ticketId)} did not require {Name(series, required)}.");
        if (addRequires.Count > 0)
            said.Add($"{Name(series, ticketId)} requires {Names(series, addRequires)} "
                + $"({await links.AddAsync(project, series.Conversation, ticketId, addRequires, cancellationToken)} new).");
        return said.Count == 0 ? new("Nothing changed: name a ticket to add or to remove as a requirement.", false) : new(string.Join(" ", said), true);
    }

    // Unstarted: the master starts it once its edges stand; the series id makes it a member, not the check's follow-up.
    private static FilingRequest Request(SeriesRunRecord series, string title, string body) =>
        new(series.Conversation, series.Principal, [], [], null, MayStartRuns: false)
        {
            FollowUp = new FilingFollowUp(title, body), IsSeriesMasterAddition = true,
        };

    private static string? AddCycle(SeriesRunRecord series, SeriesRequirementGraph graph, IReadOnlyList<string> requires,
        IReadOnlyList<string> requiredBy)
    {
        foreach (var required in requires)
            foreach (var dependent in requiredBy.Where(d => graph.Reaches(required, d)))
                return string.Equals(required, dependent, StringComparison.Ordinal)
                    ? $"Refused: the new ticket cannot both require {Name(series, required)} and be required by it; nothing was filed."
                    : $"Refused: {Name(series, required)} already waits for {Name(series, dependent)}, so a ticket between them closes a cycle; nothing was filed.";
        return null;
    }

    private static string? LinkCycle(SeriesRunRecord series, SeriesRequirementGraph graph, string ticketId, IReadOnlyList<string> addRequires)
    {
        foreach (var required in addRequires)
        {
            if (string.Equals(required, ticketId, StringComparison.Ordinal))
                return $"Refused: {Name(series, ticketId)} cannot require itself; nothing was changed.";
            if (graph.Reaches(required, ticketId))
                return $"Refused: {Name(series, required)} already waits for {Name(series, ticketId)}, so {Name(series, ticketId)} "
                    + $"requiring it closes a cycle; nothing was changed.";
            graph.Add(ticketId, required);
        }
        return null;
    }

    private static string Edges(SeriesRunRecord series, IReadOnlyList<string> requires, IReadOnlyList<string> requiredBy) =>
        (requires.Count > 0 ? $"; it requires {Names(series, requires)}" : string.Empty)
        + (requiredBy.Count > 0 ? $"; {Names(series, requiredBy)} {(requiredBy.Distinct().Count() == 1 ? "requires" : "require")} it" : string.Empty);

    private static string Names(SeriesRunRecord series, IReadOnlyList<string> ids) =>
        string.Join(", ", ids.Distinct(StringComparer.Ordinal).Select(id => Name(series, id)));

    private static string Name(SeriesRunRecord series, string ticketId) =>
        series.Tickets.FirstOrDefault(t => t.TicketId == ticketId)?.Key ?? ticketId;
}
