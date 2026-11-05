namespace AgentSmith.Contracts.Models;

/// <summary>
/// 2026-11-05-862ea: how long a work picture's truth may be, and how long the stored picture may be once serialized
/// (escaping included). In Contracts because the tool's admission rule and the column that stores it read the same numbers.
/// </summary>
public static class WorkPictureLimits
{
    public const int Truth = 300;

    public const int Stored = 2000;
}
