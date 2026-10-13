using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Structure;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using FluentAssertions;

namespace AgentSmith.Tests.Structure;

/// <summary>
/// 2026-10-13-50c7: a round's baseline scope — its own sources from the context.yaml it wrote,
/// the earlier rounds' from what they recorded, the workdir it owned from the context.yaml before.
/// </summary>
public sealed class StructureBaselineScopesTests
{
    private static BootstrapRoundContext Round(PipelineContext pipeline) =>
        new("project-bootstrap", "repo", new AgentConfig(), pipeline, "api", "src/Api");

    [Fact]
    public async Task StructureBaselineScopes_EarlierRoundRecordedSources_AreSiblingClaims()
    {
        var repo = new RepoSandbox();
        repo.Files[".agentsmith/contexts/api/context.yaml"] = "meta:\n  workdir: src/Api\nsources:\n  - src/Core\n";
        var pipeline = new PipelineContext();
        pipeline.Set<IReadOnlyDictionary<string, IReadOnlyList<DiscoveredComponent>>>(ContextKeys.DiscoveredComponents,
            new Dictionary<string, IReadOnlyList<DiscoveredComponent>>
            {
                ["repo"] = [new("api", "src/Api", "csharp", "Api.csproj"), new("web", "src/Web", "csharp", "Web.csproj")],
            });
        pipeline.Set(ContextKeys.BootstrapOutcomes, new List<BootstrapRoundOutcome>
        {
            new("repo", "web", PrinciplesMode.Transferred, [], Sources: ["src/Shared"]),
        });

        var scope = await StructureBaselineStubs.Scopes().ForAsync(
            pipeline, repo, Round(pipeline), PrinciplesMode.Refreshed, "meta:\n  workdir: src/Old\n", CancellationToken.None);

        scope.Sources.Should().Equal("src/Core");
        scope.WorkdirBefore.Should().Be("src/Old");
        scope.Siblings.Should().Equal(new StructureClaim("web", "src/Web", true), new StructureClaim("web", "src/Shared", false));
    }

    [Fact]
    public async Task StructureBaselineScopes_NoContextYaml_NoSourcesNoEarlierWorkdir()
    {
        var pipeline = new PipelineContext();

        var scope = await StructureBaselineStubs.Scopes().ForAsync(
            pipeline, new RepoSandbox(), Round(pipeline), PrinciplesMode.Transferred, "not: [yaml", CancellationToken.None);

        scope.Sources.Should().BeEmpty();
        scope.WorkdirBefore.Should().BeNull();
        scope.Siblings.Should().BeEmpty();
    }
}
