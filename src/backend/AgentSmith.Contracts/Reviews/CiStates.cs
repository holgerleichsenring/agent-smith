namespace AgentSmith.Contracts.Reviews;

/// <summary>2026-11-17-3435a: a pull request's CI on its head, as the record stores it.</summary>
public static class CiStates
{
    /// <summary>No check, status, build policy or pipeline reports on the head.</summary>
    public const string None = "none";

    /// <summary>Something still runs and nothing has failed.</summary>
    public const string Pending = "pending";

    /// <summary>2026-11-23-0219: nothing failed, and a deciding build has not started on the head and will not by itself — it
    /// has to be queued again.</summary>
    public const string NotStarted = "not_started";

    /// <summary>2026-11-23-0219: the CI has no verdict yet — it runs (<see cref="Pending"/>) or has not started
    /// (<see cref="NotStarted"/>).</summary>
    public static bool Waits(string? state) => state is Pending or NotStarted;

    /// <summary>Every deciding check passed.</summary>
    public const string Green = "green";

    /// <summary>At least one deciding check failed.</summary>
    public const string Red = "red";
}
