namespace AgentSmith.Infrastructure.Persistence.Entities;

/// <summary>
/// 2026-10-08-10b0: a repository the PR sweep knows — whether its open pull requests have been
/// recorded once (<see cref="Initialised"/>) and when it was last swept. A repository unswept for
/// two intervals is recorded again before anything starts, so a mode switch replays nothing.
/// </summary>
public sealed class PrSweepRepository : EntityBase
{
    public string Repository { get; set; } = string.Empty;
    public bool Initialised { get; set; }
    public long LastSweptTicks { get; set; }
}
