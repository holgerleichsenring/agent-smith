namespace AgentSmith.Contracts.Series;

/// <summary>2026-11-17-0207a: the states of a series run. Running and Verifying are active: the conversation's
/// messages go to the series master and the sweep starts its beats; Done and Stopped are ended. 2026-11-19-1b72a: Paused
/// is active too — the master still reads and the sweep still beats — but no ticket of the series starts. 2026-11-24-f9c5e:
/// Delivering is active: every ticket landed on the series branch and the check passed; the beat merges the series branch's
/// pull requests into the default branch, and the series is done once every one merged.</summary>
public static class SeriesRunStates
{
    public const string Running = "running";
    public const string Verifying = "verifying";
    public const string Done = "done";
    public const string Stopped = "stopped";
    public const string Paused = "paused";
    public const string Delivering = "delivering";

    /// <summary>True for a series the sweep still beats.</summary>
    public static bool IsActive(string state) => state is Running or Verifying or Paused or Delivering;
}
