namespace AgentSmith.Domain.Models;

/// <summary>
/// p0421: every phase's account of itself, in the order the phases ran.
/// <para>
/// A run delivers when each of its phases does. One phase's accounts overwriting the
/// last would make a three-phase run answerable by its final phase alone — which is the
/// same mistake as judging the branch by the last commit.
/// </para>
/// </summary>
public sealed record RunAccounts(IReadOnlyList<PhaseAccounts> Phases)
{
    public static RunAccounts Empty { get; } = new([]);

    public RunAccounts With(string phaseId, IReadOnlyList<SpecAccount> accounts) =>
        new([.. Phases.Where(p => p.PhaseId != phaseId), new PhaseAccounts(phaseId, accounts)]);

    public IReadOnlyList<SpecAccount> All => [.. Phases.SelectMany(p => p.Accounts)];
}
