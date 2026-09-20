using System.Text;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-09-07-b7e2: tells the derivation which repositories its tools may name and how a
/// fact is cited. The tool schemas travel with the request whatever the prompt says;
/// this section is what makes a name in a schema resolvable — the sandbox keys are not
/// always the repository names the code maps are listed under.
/// <para>
/// 2026-09-15-ffa7: rendered off the look it describes, so the cut review is told its own
/// allowance and its own id letter rather than the derivation's.
/// </para>
/// </summary>
internal static class DerivationLookPromptSection
{
    public static string Render(DerivationLook? look)
    {
        if (look is null) return string.Empty;
        var sb = new StringBuilder();
        sb.AppendLine();
        sb.AppendLine("## Repositories you may look into");
        sb.AppendLine(
            $"You may take up to {look.Terms.Allowance} looks with the tools offered — "
            + DerivationTools.Named(look) + " — before you write. "
            + $"Every result starts with an evidence id such as [{look.Terms.EvidencePrefix}3]; "
            + look.Terms.CiteRule);
        foreach (var repository in look.Repositories)
            sb.AppendLine($"- {repository}{Declared(look, repository)}");
        if (look.Templates.Count == 0) return sb.ToString();

        // 2026-09-13-84c0: apart from the targets, and said in the same breath as what they
        // are FOR — a template answers how work is done here, never what this ticket wants.
        sb.AppendLine();
        sb.AppendLine("## Templates this project is built after");
        sb.AppendLine(
            "These are read-only reference repositories, on their own separate look allowance "
            + $"of {look.Terms.Allowance}. A template answers HOW work is done here — "
            + "the shape a component takes, where things live, what a change touches. It never "
            + "says WHAT this ticket wants, and where the target already has a counterpart, the "
            + "target's own form wins.");
        foreach (var template in look.Templates.Keys) sb.AppendLine($"- {template}");
        return sb.ToString();
    }

    /// <summary>
    /// 2026-09-20-9c74: what a holder that may run a stage can actually ask for. A tool the
    /// holder is not told the LABELS of is a tool it spends its one run guessing at, so the
    /// labels are listed beside the repository the way the names themselves are. They are the
    /// RAW declaration: only the cannot-fail filter can be applied here, and it is the one
    /// that empties a whole repository.
    /// </summary>
    private static string Declared(DerivationLook look, string repository)
    {
        if (!look.Terms.MayRunAStage) return string.Empty;
        var labels = look.DeclaredStageLabels(repository);
        return labels.Count == 0
            ? " (declares no verify stage you may run)"
            : $" (declared verify stages you may run by label: {string.Join(", ", labels)})";
    }
}
