namespace AgentSmith.Contracts.Models.Skills;

/// <summary>
/// Activation criteria for a skill (top-level) or a role assignment (per-role).
/// A skill activates when at least one positive matches AND no negative matches.
/// </summary>
public sealed record ActivationCriteria(
    IReadOnlyList<ActivationKey> Positive,
    IReadOnlyList<ActivationKey> Negative)
{
    public static ActivationCriteria Empty { get; } =
        new ActivationCriteria(Array.Empty<ActivationKey>(), Array.Empty<ActivationKey>());
}
