using AgentSmith.Infrastructure.Persistence.Repositories;

namespace AgentSmith.Server.Services.Delivery;

/// <summary>
/// 2026-10-25-0099b: one attempt classified — its outcome, why it ended, whether a person asked for it as rework,
/// the agent seconds it counts, and what it worked on.
/// </summary>
public sealed record DeliveryAttempt(
    DeliveryAttemptRow Row,
    string Outcome,
    string? Cause,
    bool Rework,
    double Seconds,
    int SpecsDone,
    int SpecsTotal,
    IReadOnlyList<string> SpecTitles,
    IReadOnlyList<string> Repos)
{
    /// <summary>When it last did something: its finish, or now while it has not finished.</summary>
    public DateTimeOffset LastAt(DateTimeOffset now) => Row.FinishedAt ?? now;
}
