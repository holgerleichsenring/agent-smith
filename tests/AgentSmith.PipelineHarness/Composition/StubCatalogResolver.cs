using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using SkillsConfig = AgentSmith.Contracts.Models.Configuration.SkillsConfig;
using SkillsSourceMode = AgentSmith.Contracts.Models.Configuration.SkillsSourceMode;

namespace AgentSmith.PipelineHarness.Composition;

/// <summary>The skills-catalog network boundary, stubbed. Shared by the
/// p0327 durable-dialogue and p0328 expectation tests.</summary>
public sealed class StubCatalogResolver : ISkillsCatalogResolver
{
    public Task<CatalogResolution> EnsureResolvedAsync(
        SkillsConfig config, CancellationToken cancellationToken) =>
        Task.FromResult(new CatalogResolution(
            "/tmp/agentsmith-harness/empty-catalog", "harness",
            SkillsSourceMode.Default, "https://stub.test/catalog", FromCache: true));
}
