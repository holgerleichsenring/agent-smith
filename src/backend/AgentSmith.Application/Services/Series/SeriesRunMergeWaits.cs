namespace AgentSmith.Application.Services.Series;

/// <summary>2026-11-23-31dcb: what a merge step waits for. Ready: every requirement landed, its target is the default
/// branch (or not known otherwise), and no step before it is ready. After: the named tickets land first. Retarget: every
/// requirement landed but a pull request still targets another branch.</summary>
public static class SeriesRunMergeWaits
{
    public const string Ready = "ready";
    public const string After = "after";
    public const string Retarget = "retarget";
}
