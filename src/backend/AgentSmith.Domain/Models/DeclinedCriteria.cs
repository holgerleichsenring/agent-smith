namespace AgentSmith.Domain.Models;

/// <summary>
/// 2026-09-06-3d81: every criterion the run's masters declined, across its specs.
/// <para>
/// Kept per spec for the same reason <see cref="RunAccounts"/> is: the master's verdict is
/// published once per pass and overwritten by the next spec's, so without this a three-spec
/// run would report only its last spec's answers. A spec that runs again replaces its own
/// entries — a re-run that now satisfies the criterion has nothing declined any more.
/// </para>
/// </summary>
public sealed record DeclinedCriteria(IReadOnlyList<DeclinedCriterion> All)
{
    public static DeclinedCriteria Empty { get; } = new([]);

    public DeclinedCriteria With(string? specId, IReadOnlyList<DeclinedCriterion> declined) =>
        new([.. All.Where(d => !string.Equals(d.SpecId, specId, StringComparison.Ordinal)), .. declined]);
}
