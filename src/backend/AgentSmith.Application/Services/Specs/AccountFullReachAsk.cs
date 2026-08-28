using System.Text;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-08-25-6f12: the question put to the account once more, with the whole branch in
/// reach, for the criteria no diff window settled.
/// <para>
/// This is NOT the correction. The correction carries an objection about citation FORM and
/// asks the same question of the same evidence. This asks a DIFFERENT question, because the
/// evidence is different: no window's body at all, the complete file list, and search over
/// the branch and its base. A criterion whose subjects are split across windows — "each
/// applicable host contains…" — can be answered by no single window, so the union of the
/// windows' noes is not an answer about the branch, it is an artefact of the cut.
/// </para>
/// <para>
/// What it must not become is a plea. It says how to look, states plainly that a thing found
/// in one place proves nothing about the others, and asks for not satisfied where the branch
/// does not show it — because the one defect this pass could introduce is a criterion talked
/// into passing on the second attempt.
/// </para>
/// </summary>
internal static class AccountFullReachAsk
{
    public static string Message(
        IReadOnlyList<CriterionAccount> unsettled, IReadOnlyList<string>? searchable)
    {
        ArgumentNullException.ThrowIfNull(unsettled);
        var sb = new StringBuilder();
        sb.AppendLine("These criteria were judged against ONE WINDOW of the delivery at a time, and no");
        sb.AppendLine("window settled them:");
        foreach (var row in unsettled) sb.AppendLine($"- {row.Criterion}: {row.Note}");
        sb.AppendLine();
        sb.AppendLine("A criterion about EVERY host, or about more than one repository, has its subjects");
        sb.AppendLine("spread across those windows, so no single window could answer it whatever the");
        sb.AppendLine("branch carries. You are asked once more with the WHOLE branch in reach: there is");
        sb.AppendLine("no diff body below, only the complete FILE LIST and the search tools. Settle each");
        sb.AppendLine("one by LOOKING.");
        sb.AppendLine();
        Looking(sb, searchable);
        sb.AppendLine();
        sb.AppendLine("A thing found in one repository proves nothing about another, and one host that");
        sb.AppendLine("satisfies a criterion does not satisfy it for the rest — so search each one. What");
        sb.AppendLine("counts as satisfying a criterion is unchanged: cite the PATTERN you searched for,");
        sb.AppendLine("copied exactly as you wrote it, or one path exactly as the FILE LIST prints it.");
        sb.AppendLine();
        sb.AppendLine("Answer with ONLY the JSON array, for these criteria alone. Where the branch does");
        sb.AppendLine("not show it, report it not satisfied — this is a wider look, not a second chance,");
        sb.AppendLine("and an unsupported \"satisfied\" here is the one answer that misleads.");
        return sb.ToString();
    }

    /// <summary>What "looking" means, which depends on there being somewhere to look. The
    /// pass is skipped without a sandbox, so the empty arm is the belt to that brace.</summary>
    private static void Looking(StringBuilder sb, IReadOnlyList<string>? searchable)
    {
        if (searchable is not { Count: > 0 })
        {
            sb.AppendLine("Decide on the file list and the listed commands alone.");
            return;
        }
        sb.AppendLine("Search EVERY repository the criterion covers, one at a time — a criterion that");
        sb.AppendLine($"speaks of each or every host covers all of these: {string.Join(", ", searchable)}.");
        sb.AppendLine("A criterion is satisfied when every repository it covers satisfies it IN ITS OWN");
        sb.AppendLine("ROLE — where the criterion itself makes its requirement depend on what a host");
        sb.AppendLine("does, each host answers for its own part, and a repository the criterion does not");
        sb.AppendLine("cover is not a counter-example.");
        sb.AppendLine("You have a fresh search allowance for this pass, so spend it here.");
    }
}
