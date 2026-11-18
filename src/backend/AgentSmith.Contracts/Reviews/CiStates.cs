namespace AgentSmith.Contracts.Reviews;

/// <summary>2026-11-17-3435a: a pull request's CI on its head, as the record stores it.</summary>
public static class CiStates
{
    /// <summary>No check, status, build policy or pipeline reports on the head.</summary>
    public const string None = "none";

    /// <summary>Something still runs and nothing has failed.</summary>
    public const string Pending = "pending";

    /// <summary>Every deciding check passed.</summary>
    public const string Green = "green";

    /// <summary>At least one deciding check failed.</summary>
    public const string Red = "red";
}
