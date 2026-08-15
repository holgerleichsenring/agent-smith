namespace AgentSmith.Domain.Models;

/// <summary>p0421: what one phase delivered, per repository.</summary>
public sealed record PhaseAccounts(string PhaseId, IReadOnlyList<SpecAccount> Accounts);
