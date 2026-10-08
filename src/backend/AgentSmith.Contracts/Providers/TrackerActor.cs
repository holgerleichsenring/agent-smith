namespace AgentSmith.Contracts.Providers;

/// <summary>2026-10-08-2123: who acted on a tracker — the host's id, its login when it has one, and
/// whether the host marks it an app or a bot.</summary>
public sealed record TrackerActor(string Id, string? Login = null, bool IsApp = false)
{
    public bool Is(TrackerActor? other) =>
        other is not null && (string.Equals(Id, other.Id, StringComparison.OrdinalIgnoreCase)
            || (Login is not null && string.Equals(Login, other.Login, StringComparison.OrdinalIgnoreCase)));
}
