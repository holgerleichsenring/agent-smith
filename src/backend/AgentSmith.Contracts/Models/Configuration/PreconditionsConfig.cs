namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// 2026-11-16-f3fbc: how often a ticket stuck on a precondition is checked again, from agentsmith.yml's top-level
/// <c>preconditions:</c> block. A settings save re-checks at once; an init lands by a person or a pipeline and
/// nothing reports it, so the interval is what notices it. Global: the check has no project dimension.
/// </summary>
public sealed class PreconditionsConfig
{
    /// <summary>The fewest seconds between two passes.</summary>
    public const int MinRecheckIntervalSeconds = 60;

    /// <summary>The seconds between two passes over the stuck tickets.</summary>
    public int RecheckIntervalSeconds { get; set; } = 300;

    /// <summary>
    /// 2026-11-22-747dc: how many minutes before this server started an interrupted attempt may have ended and still be
    /// restarted automatically — the gap between the last server's end and this one's first pass. 0 turns the
    /// automatic restart off; a person's Retry is unaffected.
    /// </summary>
    public int InterruptedRestartWindowMinutes { get; set; } = 60;

    /// <summary>What is wrong with these values, one sentence each; empty when nothing is. The ONE rule — the load and
    /// the settings store refuse what this names, never clamping it.</summary>
    public IReadOnlyList<string> Problems()
    {
        var problems = new List<string>();
        if (RecheckIntervalSeconds < MinRecheckIntervalSeconds)
            problems.Add($"preconditions.recheck_interval_seconds is {RecheckIntervalSeconds}; it must be {MinRecheckIntervalSeconds} "
                + "or more, because each pass probes the stuck tickets' repositories over the host API, which rate-limits");
        if (InterruptedRestartWindowMinutes < 0)
            problems.Add($"preconditions.interrupted_restart_window_minutes is {InterruptedRestartWindowMinutes}; it must be 0 "
                + "(no automatic restart) or more, because it counts the minutes before this server started in which an "
                + "interrupted attempt may have ended and still be restarted");
        return problems;
    }
}
