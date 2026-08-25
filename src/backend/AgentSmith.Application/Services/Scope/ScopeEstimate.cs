using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Services.Scope;

/// <summary>
/// p0413a: what the scope call says about the TICKET rather than about the
/// repositories — how much work it is (<paramref name="Tier"/>, which sizes the
/// cost ceiling) and what kind of work it is (<paramref name="Shape"/>, which
/// sizes the cut). Scoping asks WHICH repositories and is meaningless for one;
/// the estimate is meaningful for every ticketed run, so it travels on its own
/// value instead of riding on the repo verdict.
/// </summary>
/// <param name="Tier">Unknown when the reply stated none — the cost cap then stays
/// exactly what configuration resolved.</param>
/// <param name="Shape">Null when the reply stated none — the derivation is then told
/// nothing and cuts as it did before the shape existed.</param>
public sealed record ScopeEstimate(ComplexityTier Tier, WorkShapeVerdict? Shape)
{
    /// <summary>No estimate at all — a failed call, or a reply that stated neither.</summary>
    public static ScopeEstimate None { get; } = new(ComplexityTier.Unknown, null);

    /// <summary>True when the reply stated at least one of the two.</summary>
    public bool IsStated => Tier != ComplexityTier.Unknown || Shape is not null;
}
