namespace AgentSmith.Application.Services.BaseRefresh;

/// <summary>2026-11-24-9dc0a: a path a conflict pass left open, and why — the words the master and the refusal read.</summary>
public sealed record BaseMergeOpenPath(string Path, string Why)
{
    public const string Markers = "conflict markers";
    public const string Unmerged = "still unmerged in git: stage it by name once resolved (git add, or git rm for a removal)";
    public const string Unchecked = "the server's check could not read it";

    public override string ToString() => $"{Path} ({Why})";
}
