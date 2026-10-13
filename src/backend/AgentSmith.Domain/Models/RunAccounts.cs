namespace AgentSmith.Domain.Models;

/// <summary>
/// p0421: every spec's account of itself, in the order the specs ran.
/// <para>
/// A run delivers when each of its specs does. One spec's accounts overwriting the
/// last would make a three-spec run answerable by its final spec alone — which is the
/// same mistake as judging the branch by the last commit.
/// </para>
/// </summary>
public sealed record RunAccounts(IReadOnlyList<SpecAccounts> Specs)
{
    public static RunAccounts Empty { get; } = new([]);

    public RunAccounts With(string specId, IReadOnlyList<SpecAccount> accounts) =>
        new([.. Specs.Where(p => p.SpecId != specId), new SpecAccounts(specId, accounts)]);

    public IReadOnlyList<SpecAccount> All => [.. Specs.SelectMany(p => p.Accounts)];
}
