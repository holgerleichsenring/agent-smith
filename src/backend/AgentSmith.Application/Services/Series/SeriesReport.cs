using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Series;

namespace AgentSmith.Application.Services.Series;

/// <summary>2026-11-17-0207a: what a beat posts, in order, and what the record keeps of it. 2026-11-17-0207b: the
/// opening on the series' first beat. 2026-11-25-e94eb: no status in any series conversation — no table and no
/// needs-you lines; the series page shows every ticket and what waits on a person. A beat posts the opening, what it
/// had to say (a question, an answer passed on, the check) and the master's line.</summary>
public static class SeriesReport
{
    public static IReadOnlyList<string> Messages(SeriesBeat beat, ResolvedProject project)
    {
        ArgumentNullException.ThrowIfNull(beat);
        var messages = new List<string>();
        var one = beat.Record.IsOne; // 2026-11-25-96aea: a series of one says no opening and no milestones — the ticket page shows them
        if (!one && beat.Record.LastStatusAt is null && beat.Record.IsActive) messages.Add(SeriesOpening.Text(beat, project));
        if (!one && SeriesMilestones.Merged(beat) is { } merged) messages.Add(merged); // 2026-11-25-433fa
        messages.AddRange(beat.Said);
        // 2026-11-25-e94ec: the master's line; 2026-11-26-f511c: with the beat's acts as one "↳" line under it, else their notices
        if (beat.MasterLine is { Length: > 0 } line && beat.MasterSpeaks) messages.Add(WithActs(line, beat.Acts));
        else messages.AddRange(beat.Acts.Select(a => a.Notice).OfType<string>());
        if (!one) messages.AddRange(SeriesMilestones.Stuck(beat)); // 2026-11-25-433fa
        return messages;
    }

    /// <summary>2026-11-26-f511c: the master's sentence with the acts of its beat under it: "↳ refreshed #19667 · started the CI of
    /// #19659". The dashboard draws that line small and muted; a chat platform shows it as written.</summary>
    public static string WithActs(string line, IReadOnlyList<SeriesActLine> acts)
    {
        ArgumentNullException.ThrowIfNull(acts);
        return acts.Count == 0 ? line : $"{line}\n\n↳ {string.Join(" · ", acts.Select(a => a.Did))}";
    }

    /// <summary>The record after the beat: the table it saw (what the next beat tells a change by), when it last ran
    /// (the final pull requests' poll reads it), and the messages it read.</summary>
    public static SeriesRunRecord Seen(SeriesRunRecord record, SeriesBeat beat) => record with
    {
        LastTable = beat.Table,
        LastStatusAt = beat.Now,
        MessagesThrough = Math.Max(record.MessagesThrough, beat.MessagesThrough),
    };
}
