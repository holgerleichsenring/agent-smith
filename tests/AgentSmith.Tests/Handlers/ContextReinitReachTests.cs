using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Activation;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Activation;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Skills;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Services.Activation;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Handlers;

/// <summary>
/// 2026-08-25-c9c7: the way back in for a repository that was initialised before the
/// image rule existed — this one included, and the shipped demo with it.
/// <para>
/// The claim the phase was cut on is that discovery locks such a repository out: an
/// initialised project never re-writes its context, so a write-path rule reaches new
/// projects only. That is wrong. Discovery publishes
/// <see cref="ContextKeys.DiscoveredComponents"/> for the repository's components and
/// BootstrapDispatch fans out one BootstrapRound per component — a round that reads the
/// existing context.yaml, must call write_context_yaml, and FAILS the run if it does not.
/// Re-running init-project IS the route, and the new rule is what makes the rewrite name
/// an image.
/// </para>
/// <para>
/// 2026-09-23-9bb2: the round that enumerates them is a model call on a re-init as much as
/// on a first init — the existing contexts are prior art inside its prompt. The property
/// pinned here is unchanged: what discovery answers becomes one round per component.
/// </para>
/// <para>
/// This pins that property, because it is load-bearing and was never asserted: if a
/// re-init ever stops emitting rounds, the rule silently stops reaching every repository
/// that already exists.
/// </para>
/// </summary>
public sealed class ContextReinitReachTests
{
    private const string RepoName = "monorepo";

    private static readonly ConceptVocabulary Vocab = new(new Dictionary<string, ProjectConcept>
    {
        ["pipeline_name"] = new(
            "pipeline_name", "test", ConceptType.Enum, new[] { "init-project" }, null, []),
        ["project_language"] = new("project_language", "test", ConceptType.String, null, null, []),
    });

    [Fact]
    public async Task Reinit_AnExistingProject_CanRewriteItsContext()
    {
        // A context that predates the image rule: no stack.image.
        var pipeline = PipelineWithExistingContexts(
            new RemoteContextDiscovery("server", "src/Server", "csharp"),
            new RemoteContextDiscovery("client", "client", "typescript"));

        var discovered = await Discover(DiscoveryAnswer).ExecuteAsync(
            new BootstrapDiscoverContext(RepoName, new AgentConfig(), pipeline), CancellationToken.None);
        var dispatched = await Dispatch().ExecuteAsync(
            new BootstrapDispatchContext(pipeline), CancellationToken.None);

        discovered.IsSuccess.Should().BeTrue();
        discovered.Message.Should().Contain("discovered components",
            "the round runs on a re-init and its answer is what the rounds are cut from");
        dispatched.IsSuccess.Should().BeTrue();
        dispatched.InsertNext.Should().HaveCount(2,
            "one bootstrap round per existing context — each one re-writes its context.yaml "
            + "through write_context_yaml, which is where the image rule now applies");
        dispatched.InsertNext.Select(command => command.Name)
            .Should().AllBe(CommandNames.BootstrapRound);
        dispatched.InsertNext.Select(command => command.ContextName)
            .Should().BeEquivalentTo(["server", "client"]);
    }

    private const string DiscoveryAnswer = """
        {
          "status": "complete",
          "components": [
            { "name": "server", "workdir": "src/Server", "language": "csharp",     "evidence": "src/Server/Program.cs" },
            { "name": "client", "workdir": "client",     "language": "typescript", "evidence": "client/package.json" }
          ]
        }
        """;

    private static BootstrapDiscoverHandler Discover(string answer) =>
        new(new StubChatClientFactory(new StubChatClient(new Queue<string>([answer]))),
            null, EventTestStubs.RunContext,
            new DiscoveryOutputParser(), new SandboxTargets(),
            new AgentSmith.Application.Services.Tools.AgenticToolSurface(),
            NullLogger<BootstrapDiscoverHandler>.Instance);

    private static BootstrapDispatchHandler Dispatch() =>
        new(new ActivationSkillFilter(
                new ActivationExpressionParser(new ActivationExpressionTokenizer()),
                new ActivationEvaluator(),
                NullLogger<ActivationSkillFilter>.Instance),
            context => new PipelineContextRunStateConcepts(context, Vocab),
            NullLogger<BootstrapDispatchHandler>.Instance);

    private static ProjectMap StubMap => new(
        PrimaryLanguage: "csharp",
        Frameworks: [],
        Modules: [],
        TestProjects: [],
        EntryPoints: [],
        Conventions: new Conventions(null, null, null),
        Ci: new CiConfig(false, null, null, null));

    private static PipelineContext PipelineWithExistingContexts(
        params RemoteContextDiscovery[] existing)
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.ResolvedPipeline, new ResolvedPipelineConfig(
            "init-project", new AgentConfig(), "skills", null));
        pipeline.Set<IReadOnlyList<RepoConnection>>(ContextKeys.Repos, new[]
        {
            new RepoConnection { Name = RepoName, Url = "https://x/y.git", Auth = "test" },
        });
        pipeline.Set(ContextKeys.Repository, new Repository(new BranchName("main"), "https://x/y.git"));
        pipeline.Set<IReadOnlyList<RoleSkillDefinition>>(ContextKeys.AvailableRoles, new[]
        {
            new RoleSkillDefinition
            {
                Name = "generic-bootstrap",
                ActivatesWhen = "pipeline_name = \"init-project\"",
                OutputSchema = "bootstrap",
            },
            // 2026-09-23-9bb2: the round the re-init now runs needs its own skill — a
            // catalog carrying bootstrap producers alone fails discovery before dispatch.
            new RoleSkillDefinition
            {
                Name = "project-discovery",
                ActivatesWhen = "pipeline_name = \"init-project\"",
                OutputSchema = "discovery",
            },
        });
        pipeline.Set<IReadOnlyDictionary<string, RemoteContextDiscovery>>(
            ContextKeys.SandboxDiscoveries,
            existing.ToDictionary(d => d.ContextName, d => d, StringComparer.Ordinal));
        pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(
            ContextKeys.Sandboxes,
            existing.ToDictionary(d => d.ContextName, _ => Mock.Of<ISandbox>(), StringComparer.Ordinal));
        pipeline.Set<IReadOnlyDictionary<string, ProjectMap>>(
            ContextKeys.RepoProjectMaps,
            existing.ToDictionary(d => d.ContextName, _ => StubMap, StringComparer.Ordinal));
        var concepts = new PipelineContextRunStateConcepts(pipeline, Vocab);
        concepts.SetEnum("pipeline_name", "init-project");
        concepts.SetString("project_language", "csharp");
        return pipeline;
    }
}
