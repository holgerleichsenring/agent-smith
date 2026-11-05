using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-11-04-6768a: a false premise the series' own work made false — its cited look reads absent at
/// the series base and present at HEAD (or the reverse) in <see cref="Paths"/>, and the series
/// checkpoints <see cref="Commits"/> touched those paths.
/// </summary>
public sealed record PremiseSupersession(
    PremiseFinding Finding, string Base, IReadOnlyList<string> Paths, IReadOnlyList<string> Commits);
