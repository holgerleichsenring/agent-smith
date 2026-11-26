namespace AgentSmith.Contracts.Commands;

/// <summary>2026-11-17-0207a: the steps that open a series run and beat it.</summary>
public static partial class CommandNames
{
    /// <summary>The 'file' run's last step: a filing of two or more tickets opens a series run for its conversation, a
    /// filing seeded with a series id joins that series.</summary>
    public const string OpenSeriesRun = "OpenSeriesRunCommand";

    /// <summary>2026-11-25-96aea: a code run's fourth step: a ticket no active series holds gets a series of one.</summary>
    public const string OpenTicketSeries = "OpenTicketSeriesCommand";

    /// <summary>A beat's first step: the series record, its tickets' facts, and what needs judgement.</summary>
    public const string ReadSeries = "ReadSeriesCommand";

    /// <summary>The series master's loop, run only when something needs judgement.</summary>
    public const string SeriesMaster = "SeriesMasterCommand";

    /// <summary>The beat's report into the conversation: the opening, the master's line, the table when it changed or
    /// the cadence is due.</summary>
    public const string PostSeriesStatus = "PostSeriesStatusCommand";

    /// <summary>2026-11-25-433fb: a beat's last step: past dialogue.series_master_thread_max_chars the master's older beats
    /// become its notes, its latest beats stay.</summary>
    public const string CompactSeriesThread = "CompactSeriesThreadCommand";

    /// <summary>2026-11-24-f9c5e: a beat's delivery of a series with a series branch: once every ticket landed, one pull request
    /// per repo from the series branch into the default branch, reviewed by a person and merged by the series master once the series check passed (2026-11-25-9992i).</summary>
    public const string DeliverSeries = "DeliverSeriesCommand";

    /// <summary>2026-11-17-0207d: the series-verify run's check of the series' done criteria on the default branch;
    /// failures not followed up before become one follow-up the run's filing steps file into the series.</summary>
    public const string VerifySeries = "VerifySeriesCommand";
}
