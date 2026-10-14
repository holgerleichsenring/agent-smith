using System.Text.Json;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Specs;
using AgentSmith.Infrastructure.Persistence.Entities;

namespace AgentSmith.Server.Services.Tickets;

/// <summary>
/// 2026-10-13-9a59e: which cut a ticket's series reads. A hand-back cut carries no specs, so the series
/// is the latest cut WITH specs, and a hand-back published after it is the current state above it.
/// Cuts order by their attempt's run id, then by publication.
/// </summary>
public static class TicketSeriesCuts
{
    /// <summary>The state of a spec no attempt of the cut recorded.</summary>
    public const string NotStarted = "not_started";

    /// <summary>The state of a spec the cut's attempt carried in as executed.</summary>
    public const string CarriedState = "carried";

    /// <summary>A spec's state in a cut: carried when its attempt carried it, else that attempt's RunSpec status.</summary>
    public static string StateOf(TicketSeriesRecords records, string cutRunId, IReadOnlyList<string> carried, string specId) =>
        carried.Contains(specId, StringComparer.Ordinal) ? CarriedState
            : records.Specs.LastOrDefault(r => r.SpecId == specId && r.RunId == cutRunId)?.Status ?? NotStarted;

    public static IReadOnlyList<RunSeriesCut> Ordered(IEnumerable<RunSeriesCut> cuts) =>
        [.. cuts.OrderBy(c => c.RunId, StringComparer.Ordinal).ThenBy(c => c.Id)];

    public static IReadOnlyList<SpecCutEntry> Entries(RunSeriesCut cut) =>
        RunStoryJson.TryDeserialize<List<SpecCutEntry>>(cut.SpecsJson) ?? [];

    public static RunSeriesCut? LatestWithSpecs(IReadOnlyList<RunSeriesCut> ordered) =>
        ordered.LastOrDefault(c => Entries(c).Count > 0);

    /// <summary>The newest cut when it is a hand-back — published after any cut with specs.</summary>
    public static RunSeriesCut? CurrentHandback(IReadOnlyList<RunSeriesCut> ordered) =>
        ordered.Count > 0 && ordered[^1].HandbackCase is not null ? ordered[^1] : null;

    /// <summary>The executed spec ids an attempt carried in, from its attempt record.</summary>
    public static IReadOnlyList<string> Carried(TicketSeriesRecords records, string runId) =>
        records.Attempts.GetValueOrDefault(runId)?.CarriedSpecIdsJson is { Length: > 0 } json
            ? JsonSerializer.Deserialize<List<string>>(json) ?? []
            : [];
}
