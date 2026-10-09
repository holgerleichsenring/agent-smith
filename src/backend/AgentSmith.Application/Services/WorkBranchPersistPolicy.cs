using AgentSmith.Contracts.Commands;

namespace AgentSmith.Application.Services;

/// <summary>
/// Whether a failed run had work worth saving onto a branch.
/// <para>
/// A run that COMMITS its work meant to change code, so a preset that gains a commit step gains
/// the persist with it. A run that delivers an OPINION — findings, a PR review's comments — never
/// does, and its working tree is the tree it was reading: the PR head, for a review, which the
/// persist would push onto. Running a master says nothing either way; every master preset has one.
/// </para>
/// </summary>
public static class WorkBranchPersistPolicy
{
    /// <summary>Did this pipeline mean to change code?</summary>
    public static bool IntendedToChangeCode(IReadOnlyList<string> commandNames)
    {
        ArgumentNullException.ThrowIfNull(commandNames);
        return commandNames.Contains(CommandNames.CommitAndPR)
            && !commandNames.Any(DeliversAnOpinion);
    }

    private static bool DeliversAnOpinion(string commandName) => commandName
        is CommandNames.DeliverFindings or CommandNames.CompilePrReviewFindings or CommandNames.PostPrComments;
}
