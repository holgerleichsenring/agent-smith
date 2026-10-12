using System.Text;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-10-12-3a36e: tells the derivation which repositories are in scope, by the names a
/// spec's <c>repos</c> is checked against. Rendered whether or not the derivation may look:
/// the look section lists sandbox keys and is absent without a sandbox, and neither is the
/// list a spec names its repositories from.
/// 2026-10-12-f5f7a: it states the rule — one repository per spec, work in two cut as two.
/// </summary>
internal static class ScopeRepositoriesPromptSection
{
    public static string Render(IReadOnlyList<string> scope)
    {
        if (scope.Count == 0) return string.Empty;
        var sb = new StringBuilder();
        sb.AppendLine();
        sb.AppendLine("## Repositories in scope");
        if (scope.Count == 1)
        {
            sb.AppendLine($"One repository is in scope: {scope[0]}. Every phase changes it; it is filled in for you.");
            return sb.ToString();
        }
        sb.AppendLine(
            "Every phase names in `repos` EXACTLY ONE repository: the one whose code it changes, spelled "
            + "exactly as listed here. Work that changes two repositories is cut as two phases, one per "
            + "repository, the phase that depends on the other second. A phase that declares a "
            + "`repo/context` declares only its own repository's contexts. A phase naming none, or "
            + "several, is asked again.");
        foreach (var name in scope) sb.AppendLine($"- {name}");
        return sb.ToString();
    }
}
