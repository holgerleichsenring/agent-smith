using AgentSmith.Contracts.Series;

namespace AgentSmith.Application.Services.Series;

/// <summary>
/// 2026-11-17-0207a: one beat of a series run as its steps pass it on — the record and its tickets' facts read by
/// ReadSeries, what needs the master's judgement, and what the beat has to say.
/// </summary>
public sealed class SeriesBeat(SeriesRunRecord record, IReadOnlyList<SeriesTicketFact> facts, DateTimeOffset now)
{
    public SeriesRunRecord Record { get; set; } = record;

    public IReadOnlyList<SeriesTicketFact> Facts { get; } = facts;

    public DateTimeOffset Now { get; } = now;

    /// <summary>Why the master judges this beat; empty when nothing needs it.</summary>
    public List<string> Reasons { get; } = [];

    /// <summary>Messages the beat posts into the conversation, in order.</summary>
    public List<string> Said { get; } = [];

    /// <summary>The master's closing line, posted as its own message.</summary>
    public string? MasterLine { get; set; }

    public IReadOnlyList<SeriesTableRow> Rows => SeriesTable.Rows(Facts, Now);

    public string Table => SeriesTable.Snapshot(Rows);
}
