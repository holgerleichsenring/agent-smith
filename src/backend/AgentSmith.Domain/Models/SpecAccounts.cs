namespace AgentSmith.Domain.Models;

/// <summary>p0421: what one spec delivered, per repository.</summary>
public sealed record SpecAccounts([property: AcceptedName("PhaseId")] string SpecId, IReadOnlyList<SpecAccount> Accounts);
