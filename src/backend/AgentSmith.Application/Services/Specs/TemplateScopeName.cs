using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-09-16-4df5: the ONE statement of how a template declaration is addressed.
/// <para>
/// Three places rebuilt this from a prefix and a context name by hand, and they behaved
/// differently on the same input: the scope selection discarded a duplicate, the spec-dialog
/// stamp RAISED on it, and the proof report keyed its evidence by it and proved the wrong
/// template. A project whose Client and BackgroundWorker both declare "default" hit all three.
/// </para>
/// <para>
/// A declaration that names its local repository is addressed
/// <c>template:&lt;repo&gt;/&lt;context&gt;</c>; one that does not keeps <c>template:&lt;context&gt;</c>.
/// Two shapes on one rule: an unqualified declaration has no repository to put in a name, and
/// keeping its shape is what leaves every stored configuration addressed exactly as it is
/// today. The path router resolves the longest registered key followed by a slash — it was
/// built for nested group paths — so the two forms coexist.
/// </para>
/// </summary>
public static class TemplateScopeName
{
    /// <summary>How a template entry is addressed, so a model can tell one from a target.</summary>
    public const string Prefix = "template:";

    /// <summary>The address of a declaration of <paramref name="context"/>.</summary>
    public static string For(string context, string? contextRepo) =>
        string.IsNullOrWhiteSpace(contextRepo)
            ? $"{Prefix}{context}"
            : $"{Prefix}{contextRepo}/{context}";

    /// <inheritdoc cref="For(string, string?)"/>
    public static string For(ProjectTemplate template)
    {
        ArgumentNullException.ThrowIfNull(template);
        return For(template.Context, template.ContextRepo);
    }
}
