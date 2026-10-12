using System.Text;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-10-12-3a36e: tells the derivation which repositories are in scope, by the names a
/// phase's <c>repos</c> is checked against. Rendered whether or not the derivation may look:
/// the look section lists sandbox keys and is absent without a sandbox, and neither is the
/// list a phase names its repositories from.
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
            "Every phase names the repositories it changes in `repos`, spelled exactly as listed here. "
            + "When one phase names them, every phase must; a phase that declares a `repo/context` "
            + "names that repository too. A reply that names none is read as every phase changing all of them.");
        foreach (var name in scope) sb.AppendLine($"- {name}");
        return sb.ToString();
    }
}
