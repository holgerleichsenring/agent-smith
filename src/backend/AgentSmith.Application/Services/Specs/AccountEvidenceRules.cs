namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-08-25-1360: what the account is TOLD, separated from what it is SHOWN.
/// <para>
/// The prompt assembles evidence — criteria, a file list, a body, a command list. These are
/// the standing rules for reading that evidence, and they change for different reasons: a
/// rule moves when the account is reasoning wrongly, the assembly moves when the account is
/// being handed the wrong thing. Keeping them in one file is also what pushed it past the
/// length the architecture rule allows, which is the rule noticing the same seam.
/// </para>
/// </summary>
internal static class AccountEvidenceRules
{
    /// <summary>
    /// p0483: an absence is settled by LOOKING, not by being shown a command that looked.
    /// Without a sandbox there is nothing to look with, and the account is told so rather
    /// than being invited to search something that cannot answer.
    /// </summary>
    public static string Absence(IReadOnlyList<string>? searchable) =>
        searchable is { Count: > 0 }
            ? "A criterion about something being ABSENT you settle YOURSELF: call search_branch\n"
              + "against each repository the criterion covers and read what comes back. No\n"
              + "output means the branch does not contain it, and that is the proof. The\n"
              + $"repositories you can search are: {string.Join(", ", searchable)}. Do not close\n"
              + "an absence criterion on a listed command when you could look instead, and do\n"
              + "not report one as unsatisfied without having searched for it. A criterion you\n"
              + "settled this way is CITED BY THE PATTERN you searched for, copied exactly as\n"
              + "you wrote it — that search is evidence like any command, and a criterion you\n"
              + "searched and then cite nothing for is refused."
            : "A criterion about something being ABSENT is answered by the commands listed\n"
              + "under COMMANDS: no diff shows what a repository does NOT contain.";
}
