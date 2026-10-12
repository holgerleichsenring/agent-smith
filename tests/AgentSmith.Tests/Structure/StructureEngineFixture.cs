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
}
