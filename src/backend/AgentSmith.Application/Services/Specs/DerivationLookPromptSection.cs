using System.Text;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-09-07-b7e2: tells the derivation which repositories its tools may name and how a
/// fact is cited. The tool schemas travel with the request whatever the prompt says;
/// this section is what makes a name in a schema resolvable — the sandbox keys are not
/// always the repository names the code maps are listed under.
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
            $"You may take up to {DerivationLookBudget.Allowance} looks with the tools offered — "
            + "a search, a file read, the ecosystem's own dependency audit — before you write. "
            + "Every result starts with an evidence id such as [L3]; a fact you state cites "
            + "that id, and a fact that cites none is recorded as an assumption.");
        foreach (var repository in look.Repositories) sb.AppendLine($"- {repository}");
        if (look.Templates.Count == 0) return sb.ToString();

        // 2026-09-13-84c0: apart from the targets, and said in the same breath as what they
        // are FOR — a template answers how work is done here, never what this ticket wants.
        sb.AppendLine();
        sb.AppendLine("## Templates this project is built after");
        sb.AppendLine(
            "These are read-only reference repositories, on their own separate look allowance "
            + $"of {DerivationLookBudget.Allowance}. A template answers HOW work is done here — "
            + "the shape a component takes, where things live, what a change touches. It never "
            + "says WHAT this ticket wants, and where the target already has a counterpart, the "
            + "target's own form wins.");
        foreach (var template in look.Templates.Keys) sb.AppendLine($"- {template}");
        return sb.ToString();
    }
}
