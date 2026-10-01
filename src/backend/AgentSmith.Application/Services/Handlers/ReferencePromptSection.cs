using AgentSmith.Application.Services.Specs;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-10-01-283dc: tells the master what a <c>reference:</c> address IS — a website the operator
/// uploaded to this conversation, read-only, read with the tools a repository is read with. Built
/// over the address list like <see cref="TemplatePromptSection"/>, and empty when there is none.
/// </summary>
internal static class ReferencePromptSection
{
    internal static string Build(IReadOnlyList<string> addresses)
    {
        var references = addresses?
            .Where(a => a.StartsWith(ReferenceScopeName.Prefix, StringComparison.Ordinal))
            .ToList();
        if (references is null || references.Count == 0) return string.Empty;
        var bullets = string.Join("\n", references.Select(n => $"- `{n}`"));
        return "\n\n## Websites the operator uploaded\n"
            + "These addresses are a website the operator uploaded to this conversation — its "
            + "HTML, CSS, scripts and assets at their own paths. They are READ-ONLY: read them "
            + "with read_file, grep and directory_tree; a write comes back refused.\n"
            + bullets + "\n\n"
            + "Its CSS states exact values — colours, sizes, spacing, fonts. When the operator "
            + "asks for something to look like it, read the value there rather than estimating "
            + "it from a screenshot.\n";
    }
}
