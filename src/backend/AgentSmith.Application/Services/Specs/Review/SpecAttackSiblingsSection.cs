using System.Text;

namespace AgentSmith.Application.Services.Specs.Review;

/// <summary>
/// 2026-11-11-2bdfa: the other specs of the epic a design turn's draft belongs to, rendered just before the draft
/// itself — the per-draft tail of the attack prompt, so the shared prefix stays cacheable. Empty for a lone spec.
/// </summary>
public static class SpecAttackSiblingsSection
{
    public const string Heading = "## The other specs of this epic";

    public const string Rule =
        "These specs are proposed beside the one under review. A capability one of them takes is not dropped or "
        + "postponed by this spec.";

    public static string Render(IReadOnlyList<SpecAttackSibling> siblings)
    {
        ArgumentNullException.ThrowIfNull(siblings);
        if (siblings.Count == 0) return string.Empty;
        var sb = new StringBuilder().AppendLine().AppendLine(Heading).AppendLine(Rule);
        foreach (var sibling in siblings)
        {
            sb.AppendLine().AppendLine($"- {sibling.SpecId}: {sibling.Goal}");
            if (sibling.Repos.Count > 0) sb.AppendLine($"  repos: {string.Join(", ", sibling.Repos)}");
            if (!string.IsNullOrWhiteSpace(sibling.ScopeIn)) sb.AppendLine($"  scope in: {sibling.ScopeIn.Trim()}");
            if (!string.IsNullOrWhiteSpace(sibling.ScopeOut)) sb.AppendLine($"  scope out: {sibling.ScopeOut.Trim()}");
        }
        return sb.ToString();
    }
}
