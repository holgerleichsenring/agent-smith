using AgentSmith.Application.Services.Preflight.Run;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Application.Services.Scope;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.TestHelpers;

/// <summary>2026-11-16-f3fba: the pre-sandbox context inventory the image preflight checks read.</summary>
internal static class PreflightInventories
{
    public static RunContextInventory Over(ISandboxLanguageResolver? resolver = null)
    {
        resolver ??= Mock.Of<ISandboxLanguageResolver>();
        return new RunContextInventory(
            new RemoteContextInventoryBuilder(resolver, NullLogger<RemoteContextInventoryBuilder>.Instance), resolver);
    }

    /// <summary>The run's one repository and the inventory ScopeRepos would have cached for it.</summary>
    public static void Seed(PipelineContext pipeline, string repo, params RemoteContextDiscovery[] contexts)
    {
        pipeline.Set<IReadOnlyList<RepoConnection>>(ContextKeys.Repos, [new RepoConnection { Name = repo }]);
        pipeline.Set<IReadOnlyDictionary<string, IReadOnlyList<RemoteContextDiscovery>>>(
            ContextKeys.RemoteContextInventory,
            new Dictionary<string, IReadOnlyList<RemoteContextDiscovery>> { [repo] = contexts });
    }

    public static SandboxContainerRuntime Containers { get; } = new(SpawnsContainers: true);
}
