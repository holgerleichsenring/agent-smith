using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services;

/// <summary>
/// 2026-09-23-9bb2: what the repository already declares under <c>.agentsmith/contexts/</c>,
/// stated to the discovery round as PRIOR ART — the components the LAST derivation concluded,
/// not facts about the tree being read now.
/// <para>
/// A re-init used to return the declaration instead of deriving again, so a component whose
/// workdir was derived wrongly kept that workdir through every run of the command that produced
/// it. Stated this way the round keeps what the tree still proves and corrects what it does not.
/// </para>
/// </summary>
internal static class BootstrapDiscoverPriorArt
{
    /// <summary>The prompt section, or an empty string when the repository declares nothing.</summary>
    public static string Section(IReadOnlyList<DiscoveredComponent> declared)
    {
        if (declared is null || declared.Count == 0) return string.Empty;
        var lines = string.Join("\n", declared.Select(c =>
            $"- `{c.Name}` — workdir `{c.Workdir}`, language `{Language(c)}`"));
        return $"""


            ## Previously derived (already written into `.agentsmith/contexts/`)

            A previous run of this command concluded the components below and wrote them into
            this repository. They are what that derivation CONCLUDED — not ground truth, and not
            a list to repeat back:

            {lines}

            Derive from the tree as you would with no such list, then use it: keep an entry the
            tree still proves, correct a name, workdir or language it does not, drop one whose
            component is gone, and add a component the criterion below proves and this list
            missed — being absent from this list makes nothing a component that the criterion
            excludes. A workdir here that does not match where the source actually sits is the
            value to fix.
            """;
    }

    private static string Language(DiscoveredComponent component) =>
        string.IsNullOrWhiteSpace(component.Language) ? "unstated" : component.Language;
}
