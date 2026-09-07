namespace AgentSmith.Contracts.Models;

/// <summary>
/// The package ecosystem a repository declares through its marker files, and the
/// marker that declared it. One detection serves two readers: the security scan's
/// dependency audit and the derivation's audit tool, so that "which audit applies
/// here" has one answer.
/// </summary>
public sealed record PackageEcosystem(PackageEcosystemKind Kind, string Marker);
