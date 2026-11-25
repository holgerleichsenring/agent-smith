using AgentSmith.Contracts.Series;

namespace AgentSmith.Application.Services.Series;

/// <summary>
/// 2026-11-17-0207a: one beat of a series run as its steps pass it on — the record and its tickets' facts read by
/// ReadSeries, what needs the master's judgement, and what the beat has to say.
/// </summary>
public sealed class SeriesBeat(SeriesRunRecord record, IReadOnlyList<SeriesTicketFact> facts, DateTimeOffset now)
{
    public SeriesRunRecord Record { get; set; } = record;

    /// <summary>2026-11-25-9992f: settable — a ticket the master adds in this beat is a member its next tool call can name.</summary>
    public IReadOnlyList<SeriesTicketFact> Facts { get; set; } = facts;

    public DateTimeOffset Now { get; } = now;

    /// <summary>2026-11-25-433fa: the series' state when the beat read it — a beat that moved it (the check, delivering, done)
    /// tells every member's page at its end.</summary>
    public string StartState { get; } = record.State;

    /// <summary>2026-11-25-9992h: the name the series acts under — its project's machine identity, agent-smith without one.</summary>
    public string Actor { get; set; } = Contracts.Runs.AttemptChannels.SeriesActor;

    /// <summary>2026-11-25-9992e: what the act under way did, in words ("merged SCRUM-12 (now)"), set by
    /// <see cref="SeriesActSay"/> and logged with the act by <see cref="SeriesActedFunction"/>; null before an act says it.</summary>
    public string? ActDid { get; set; }

    /// <summary>2026-11-25-9992f: the ticket the act under way acted on when its arguments name none (an added ticket's id);
    /// logged in place of the call's ticket argument.</summary>
    public string? ActTicket { get; set; }

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

    /// <summary>2026-11-25-433fb: the master judged this beat and its thread grew by it — only then may the thread need
    /// compacting.</summary>
    public bool MasterJudged { get; set; }

    /// <summary>2026-11-25-e94ec: the master called an act this beat (<see cref="SeriesActedFunction"/>).</summary>
    public bool MasterActed { get; set; }

    /// <summary>2026-11-25-e94ec: the master's own question was answered or taken by default this beat.</summary>
    public bool MasterAnswered { get; set; }

    /// <summary>2026-11-25-e94ec: whether the master's line reaches the conversation — it acted, its question was
    /// answered, or the operator wrote to it; never for a beat in which it only looked. 2026-11-25-9992g: a person's comment
    /// on a member ticket speaks to it like an operator message.</summary>
    public bool MasterSpeaks => MasterActed || MasterAnswered || Messages.Count > 0 || Wakes.Any(w => w.Kind == SeriesWakeKinds.Comment);

    /// <summary>2026-11-25-9992g: the budget question was answered this beat with something that is no choice; it is asked
    /// again in a later beat, never in the one the master answers it.</summary>
    public bool BudgetHeard { get; set; }

    /// <summary>2026-11-25-9992g: the wakes this beat hands the master — none for a paused series, which keeps them.</summary>
    public List<SeriesWake> Wakes { get; } = [];

    /// <summary>2026-11-25-9992g: the stored wakes once the master judged this beat's — exactly the ones it was handed are
    /// cleared; any other, recorded meanwhile or not handed over, stays for the next beat.</summary>
    public IReadOnlyList<SeriesWake> Unjudged(IReadOnlyList<SeriesWake> stored)
    {
        ArgumentNullException.ThrowIfNull(stored);
        return Wakes.Count == 0 ? stored : [.. stored.Where(w => !Wakes.Contains(w))];
    }

    public IReadOnlyList<SeriesTableRow> Rows => SeriesTable.Rows(Facts, Now);

    /// <summary>2026-11-25-9992a: the keys of the tickets a run of the series is at work on now.</summary>
    public IReadOnlyList<string> WorkingOn => SeriesWorkingOn.Of(Facts);

    public string Table => SeriesTable.Snapshot(Rows);
}
