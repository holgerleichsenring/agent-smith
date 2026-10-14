namespace AgentSmith.Contracts.Sandbox;

/// <summary>
/// 2026-10-13-9a59i: how a recorded sandbox ended. The first end wins; <see cref="Released"/> is
/// transient — the run's end names it (park, requeue or end), and a row no close event reached
/// reads <see cref="Unobserved"/>.
/// </summary>
public static class SandboxEndReasons
{
    public const string Exited = "exited";
    public const string OomKilled = "oom-killed";
    public const string Vanished = "vanished";
    public const string Gone = "gone";
    public const string Released = "released";
    public const string ReleasedAtPark = "released-at-park";
    public const string ReleasedAtRequeue = "released-at-requeue";
    public const string ReleasedAtEnd = "released-at-end";
    public const string Unobserved = "unobserved";
}
