using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Series;

namespace AgentSmith.Application.Services.Series;

/// <summary>
/// 2026-11-25-96aeb: the series master's merge of one member ticket — checked by the API, judged with an override reason — said
/// as what it did: merged (with the refusal it went past and the override), refused, or refused in part. An override that went
/// past a refusal is said even without a reason, so the act log, the ticket's last act and the conversation carry it.
/// </summary>
public sealed class SeriesTicketMerge(SeriesBeat beat, ISeriesPullRequestActs acts)
{
    private const string MergedWord = "merged ";

    /// <summary>The act merged its ticket (every repository merged or armed), as its text says.</summary>
    public static bool Merged(SeriesAct? act) =>
        act is { Tool: SeriesPullRequestTools.MergeTool } && act.Text.StartsWith(MergedWord, StringComparison.Ordinal);

    public async Task<string> MergeAsync(
        SeriesTicketFact fact, string mode, BranchProtection protection, string? reason, string? given, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(fact);
        var outcome = await acts.MergeAsync(beat.Record, fact.Ticket.TicketId, protection, given, ct);
        var key = fact.Ticket.Key;
        var did = outcome.Moved
            ? $"{MergedWord}{key} ({mode}){(given is not null && outcome.Overrode ? $" over its refusal; override: {given}" : string.Empty)}"
            : outcome.Partly ? $"merge of {key} refused in part" : $"merge of {key} refused";
        if (string.IsNullOrWhiteSpace(reason) && given is not null && outcome.Overrode) return SeriesActSay.Told(beat, did, outcome.Answer);
        return SeriesActSay.Acted(beat, did, string.IsNullOrWhiteSpace(reason) ? given : reason, outcome.Answer);
    }
}
