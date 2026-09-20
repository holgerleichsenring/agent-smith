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

    /// <summary>
    /// How two addresses are compared. The discovery filter that admits a context and the
    /// catalog that validates a repository ref are both case-insensitive, so two spellings of
    /// one declaration denote ONE address here too. The composed name keeps the casing it was
    /// written with — an operator and the model are shown it — and only the comparison folds.
    /// </summary>
    public static StringComparer Comparer => StringComparer.OrdinalIgnoreCase;

    /// <summary>
    /// Whether the declaration at <paramref name="ordinal"/> OWNS the address it composes: an
    /// address belongs to the FIRST declaration, in the project's own declaration order, that
    /// composes it. Two declarations of one address share ONE materialised scope, so without
    /// this every one of them was read out of the owner's checkout and the wrong read was
    /// minted as evidence.
    /// </summary>
    /// <param name="declarations">The project's FULL declaration list. A reader that filters
    /// first and a reader that does not must get one answer, and they only do when the list
    /// ownership is computed over is the same list.</param>
    public static bool Owns(IReadOnlyList<ProjectTemplate> declarations, int ordinal)
    {
        ArgumentNullException.ThrowIfNull(declarations);
        var address = For(declarations[ordinal]);
        for (var earlier = 0; earlier < ordinal; earlier++)
            if (Comparer.Equals(For(declarations[earlier]), address))
                return false;
        return true;
    }
}
