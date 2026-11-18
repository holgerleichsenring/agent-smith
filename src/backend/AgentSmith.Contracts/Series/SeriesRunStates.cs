namespace AgentSmith.Contracts.Series;

/// <summary>2026-11-17-0207a: the states of a series run. Running and Verifying are active: the conversation's
/// messages go to the series master and the sweep starts its beats; Done and Stopped are ended.</summary>
public static class SeriesRunStates
{
    public const string Running = "running";
    public const string Verifying = "verifying";
    public const string Done = "done";
    public const string Stopped = "stopped";

    /// <summary>True for a series the sweep still beats.</summary>
    public static bool IsActive(string state) => state is Running or Verifying;
}
