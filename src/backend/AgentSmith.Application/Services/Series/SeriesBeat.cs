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

    /// <summary>2026-11-17-0207b: the operator's messages to the series this beat read, and the transcript index read
    /// through — the record keeps it once the beat reported.</summary>
    public List<SeriesOperatorMessage> Messages { get; } = [];

    public int MessagesThrough { get; set; } = record.MessagesThrough;

    /// <summary>The master's closing line, posted as its own message when <see cref="MasterSpeaks"/>.</summary>
    public string? MasterLine { get; set; }

    /// <summary>2026-11-25-e94ec: the master called an act this beat (<see cref="SeriesActedFunction"/>).</summary>
    public bool MasterActed { get; set; }

    /// <summary>2026-11-25-e94ec: the master's own question was answered or taken by default this beat.</summary>
    public bool MasterAnswered { get; set; }

    /// <summary>2026-11-25-e94ec: whether the master's line reaches the conversation — it acted, its question was
    /// answered, or the operator wrote to it; never for a beat in which it only looked.</summary>
    public bool MasterSpeaks => MasterActed || MasterAnswered || Messages.Count > 0;

    public IReadOnlyList<SeriesTableRow> Rows => SeriesTable.Rows(Facts, Now);

    public string Table => SeriesTable.Snapshot(Rows);
}
