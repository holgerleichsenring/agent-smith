namespace AgentSmith.Contracts.Commands;

/// <summary>2026-11-17-0207a: the steps that open a series run and beat it.</summary>
public static partial class CommandNames
{
    /// <summary>The 'file' run's last step: a filing of two or more tickets opens a series run for its conversation, a
    /// filing seeded with a series id joins that series.</summary>
    public const string OpenSeriesRun = "OpenSeriesRunCommand";

    /// <summary>A beat's first step: the series record, its tickets' facts, and what needs judgement.</summary>
    public const string ReadSeries = "ReadSeriesCommand";

    /// <summary>The series master's loop, run only when something needs judgement.</summary>
    public const string SeriesMaster = "SeriesMasterCommand";

    /// <summary>The beat's report into the conversation: the opening, the master's line, the table when it changed or
    /// the cadence is due.</summary>
    public const string PostSeriesStatus = "PostSeriesStatusCommand";

    /// <summary>2026-11-17-0207d: the series-verify run's check of the series' done criteria on the default branch;
    /// failures not followed up before become one follow-up the run's filing steps file into the series.</summary>
    public const string VerifySeries = "VerifySeriesCommand";
}
