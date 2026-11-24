namespace AgentSmith.Application.Services.BaseRefresh;

/// <summary>2026-11-24-9dc0a: what a conflict pass leaves — the paths the server's check still finds open (none: the
/// merge is whole and may be committed), the master's reason, and the paths its file tools wrote.</summary>
public sealed record BaseMergeResolution(IReadOnlyList<string> Open, string? Reason, IReadOnlyList<string> Wrote)
{
    public bool IsWhole => Open.Count == 0;
}
