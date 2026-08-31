using FluentAssertions;

namespace AgentSmith.Tests.Architecture;

/// <summary>
/// 2026-08-31-77a8: the domain profile put OUR copy of a claim about somebody else's
/// estate into an artefact we release and version, and the binary policed the taxonomy —
/// an unknown domain word refused a run before any sandbox existed. It was the third
/// instance of an idea that had already lost twice, so it is gone rather than tuned.
/// <para>
/// A removal is only finished when it cannot come back by halves: a leftover type, a
/// leftover catalog reader or a leftover reference to the word is what a later phase
/// would build the fourth instance on top of.
/// </para>
/// </summary>
public sealed class ProfileMechanismRetiredTests
{
    private static readonly string[] Retired =
    [
        "DomainProfile",
        "IDomainProfileCatalog",
        "FileDomainProfileCatalog",
        "DomainProfileStages",
        "ContextDomainResolver",
        "ContextDomainCheck",
        "meta.domain",
    ];

    [Fact]
    public void NoProfileTypeOrCatalogReaderRemains()
    {
        var found = ArchitectureSources.HandWrittenBackendFiles()
            .SelectMany(Hits)
            .OrderBy(hit => hit, StringComparer.Ordinal)
            .ToList();

        found.Should().BeEmpty(
            "no profile type, catalog reader or domain word survives the removal — a "
            + "half-alive mechanism is what the fourth instance gets built on.\n  "
            + string.Join("\n  ", found));
    }

    /// <summary>
    /// The key is DEPRECATED in the schema, not deleted from it: the root is
    /// <c>additionalProperties: false</c>, so removing the property would refuse every
    /// context written before this phase instead of merely no longer asking for one.
    /// </summary>
    [Fact]
    public void TheSchemaStillDeclaresDomain_AsDeprecated()
    {
        ContextSchemaFile.DeclaredKeys("meta").Should().Contain("domain");
        ContextSchemaFile.Root["properties"]!["meta"]!["properties"]!["domain"]!["$comment"]!
            .GetValue<string>().Should().Contain("DEPRECATED");
    }

    private static IEnumerable<string> Hits(string file)
    {
        var text = File.ReadAllText(file);
        var relative = Path.GetRelativePath(ArchitectureSources.BackendRoot, file).Replace('\\', '/');
        return Retired
            .Where(name => text.Contains(name, StringComparison.Ordinal))
            .Select(name => $"{relative}: {name}");
    }
}
