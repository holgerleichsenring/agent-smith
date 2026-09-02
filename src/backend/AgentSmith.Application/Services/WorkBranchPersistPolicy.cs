using AgentSmith.Contracts.Commands;

namespace AgentSmith.Application.Services;

/// <summary>
/// 2026-09-02-de87: whether a failed run had work worth saving onto a branch.
/// <para>
/// The question used to be answered by a list of code-modifying handler names, and that list
/// answered wrongly for every shipped preset, in both directions. AgenticExecute, GenerateTests
/// and GenerateDocs are in no preset at all, and the coding preset drives its master inside
/// PhaseSequence — so it matched NOTHING and the persist that exists for coding runs never ran
/// for one. What it did match was AgenticMaster, which the two SCAN presets carry: a failing scan
/// persisted a work branch and staged the tree it had been reading, an operator's uncommitted
/// changes included.
/// </para>
/// <para>
/// Two marks answer it exactly, and both state something true of the run rather than enumerating
/// the handlers that happen to write files. A run that DELIVERS FINDINGS produces an opinion and
/// never a diff. A run that COMMITS meant to change code — so a preset that later gains a commit
/// step gains the persist with it, without anyone remembering to add a name to a list.
/// </para>
/// </summary>
public static class WorkBranchPersistPolicy
{
    /// <summary>Did this pipeline mean to change code?</summary>
    public static bool IntendedToChangeCode(IReadOnlyList<string> commandNames)
    {
        ArgumentNullException.ThrowIfNull(commandNames);
        return !commandNames.Contains(CommandNames.DeliverFindings)
            && (commandNames.Contains(CommandNames.CommitAndPR)
                || ContainsCodeModifyingHandler(commandNames));
    }

    /// <summary>
    /// The legacy names. Dead in every preset, alive on the skill-manager and autonomous paths,
    /// which compose command lists of their own — kept behind the two marks rather than deleted,
    /// because removing them would change behaviour nobody has measured.
    /// </summary>
    private static bool ContainsCodeModifyingHandler(IReadOnlyList<string> commandNames) =>
        commandNames.Any(n => n == CommandNames.AgenticExecute
                           || n == CommandNames.AgenticMaster
                           || n == CommandNames.GenerateTests
                           || n == CommandNames.GenerateDocs);
}
