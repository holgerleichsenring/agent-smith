namespace AgentSmith.Domain.Models;

/// <summary>p0421: what one spec delivered, per repository.</summary>
public sealed record PhaseAccounts([property: AcceptedName("SpecId")] string PhaseId, IReadOnlyList<SpecAccount> Accounts);
