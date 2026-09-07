namespace AgentSmith.Contracts.Models;

/// <summary>
/// 2026-09-07-b7e2: one fact the derivation stated about the repository, together with
/// the framework-minted evidence line it cites — <c>[L3] api: the derivation ran '…'
/// exited 0</c>. A fact carries its evidence so a reader who sees "one direct package is
/// affected" can ask "which look said so?" and be answered.
/// </summary>
public sealed record PhaseFact(string Claim, string Evidence);
