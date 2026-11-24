namespace AgentSmith.Application.Services.Pocs.Rollout;

/// <summary>2026-11-23-c81ec: what one poll of a rollout follow decided.</summary>
public enum PocRolloutReadingKind
{
    Waiting,
    Settled,
    Failed,
}
