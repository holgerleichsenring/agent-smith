using AgentSmith.Infrastructure.Services.Structure;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Structure;

/// <summary>2026-10-06-8565b: an engine over the embedded language data, built without DI.</summary>
internal static class StructureEngineFixture
{
    public static TreeSitterStructureEngine Engine(StructureLanguageCatalog? catalog = null) =>
        new(catalog ?? new StructureLanguageCatalog(),
            new StructureGrammarLoader(NullLogger<StructureGrammarLoader>.Instance),
            new StructureUnitCollector());

    /// <summary>C# claimed by a language whose grammar library does not exist.</summary>
    public static TreeSitterStructureEngine MissingGrammar() => Engine(new StructureLanguageCatalog([new StructureLanguage
    {
        Name = "missing", Extensions = [".cs"], Library = "tree-sitter-does-not-exist",
        Symbol = "tree_sitter_does_not_exist", Query = "(x) @type",
    }]));
}
