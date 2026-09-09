namespace AgentSmith.Domain.Models;

/// <summary>
/// 2026-09-06-3d81: every criterion the run's masters declined, across its phases.
/// <para>
/// Kept per phase for the same reason <see cref="RunAccounts"/> is: the master's verdict is
/// published once per pass and overwritten by the next phase's, so without this a three-phase
/// run would report only its last phase's answers. A phase that runs again replaces its own
/// entries — a re-run that now satisfies the criterion has nothing declined any more.
/// </para>
/// </summary>
public sealed record DeclinedCriteria(IReadOnlyList<DeclinedCriterion> All)
{
    public static DeclinedCriteria Empty { get; } = new([]);

    public DeclinedCriteria With(string? phaseId, IReadOnlyList<DeclinedCriterion> declined) =>
        new([.. All.Where(d => !string.Equals(d.PhaseId, phaseId, StringComparison.Ordinal)), .. declined]);
}
