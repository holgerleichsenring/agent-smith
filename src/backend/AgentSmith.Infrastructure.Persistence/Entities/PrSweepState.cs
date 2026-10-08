namespace AgentSmith.Infrastructure.Persistence.Entities;

/// <summary>
/// 2026-10-08-10b0: what the PR sweep last acted on for one open pull request — the head it
/// reviewed, whether the review label was present, and the newest comment it handed to command
/// admission (host time, then the host's id for two comments of one second).
/// </summary>
public sealed class PrSweepState : EntityBase
{
    public string Repository { get; set; } = string.Empty;
    public string Number { get; set; } = string.Empty;
    public string? ReviewedHead { get; set; }
    public bool LabelPresent { get; set; }
    public long CommentsSeenTicks { get; set; }
    public string CommentsSeenId { get; set; } = string.Empty;
}
