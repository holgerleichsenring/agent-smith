using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Activation;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Activation;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Skills;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Services.Activation;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Handlers;

public sealed class BootstrapDispatchHandlerTests
{
    // p0155: project_language is a free-form string; the LLM owns the slug
    // vocabulary via the project-analyzer prompt.
    private static readonly ConceptVocabulary Vocab = new(new Dictionary<string, ProjectConcept>
    {
        ["pipeline_name"] = new(
            "pipeline_name", "test", ConceptType.Enum,
            new[] { "init-project", "fix-bug", "security-scan" }, null, []),
        ["project_language"] = new(
            "project_language", "test", ConceptType.String,
            null, null, []),
    });

    private readonly ActivationSkillFilter _filter = new(
        new ActivationExpressionParser(new ActivationExpressionTokenizer()),
        new ActivationEvaluator(),
        NullLogger<ActivationSkillFilter>.Instance);

    private readonly Func<PipelineContext, IRunStateConcepts> _conceptsFactory =
        ctx => new PipelineContextRunStateConcepts(ctx, Vocab);

    private BootstrapDispatchHandler Handler() => new(
        _filter, _conceptsFactory, NullLogger<BootstrapDispatchHandler>.Instance);

    [Fact]
    public async Task ExecuteAsync_SingleMatch_EmitsBootstrapRound()
    {
        var pipeline = PipelineFor("init-project", "csharp",
            new RoleSkillDefinition
            {
                Name = "csharp-bootstrap",
                ActivatesWhen = "pipeline_name = \"init-project\" AND project_language = \"csharp\"",
            },
            new RoleSkillDefinition
            {
                Name = "node-bootstrap",
                ActivatesWhen = "pipeline_name = \"init-project\" AND project_language = \"node\"",
            });

        var result = await Handler().ExecuteAsync(
            new BootstrapDispatchContext(pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.InsertNext.Should().HaveCount(1);
        var emitted = result.InsertNext[0];
        // p0130c-followup: dispatch now emits BootstrapRoundCommand (tool-bearing
        // producer loop) instead of SkillRoundCommand (observation-only).
        emitted.Name.Should().Be(CommandNames.BootstrapRound);
        emitted.SkillName.Should().Be("csharp-bootstrap");
        emitted.Round.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_NoMatch_FailMessage_IncludesObservedLanguageAndAvailableSkillNames()
    {
        // Catalog contains node + generic bootstrap; project_language=python → no match.
        // p0155: failure must surface both the observed slug AND the available skill
        // names so the operator can diagnose missing skill vs typo'd analyzer output.
        var pipeline = PipelineFor("init-project", "python",
            new RoleSkillDefinition
            {
                Name = "node-bootstrap",
                ActivatesWhen = "pipeline_name = \"init-project\" AND project_language = \"node\"",
            },
            new RoleSkillDefinition
            {
                Name = "generic-bootstrap",
                ActivatesWhen = "pipeline_name = \"init-project\" AND project_language = \"generic\"",
            });

        var result = await Handler().ExecuteAsync(
            new BootstrapDispatchContext(pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("python");
        result.Message.Should().Contain("node-bootstrap");
        result.Message.Should().Contain("generic-bootstrap");
    }

    [Fact]
    public async Task ExecuteAsync_AmbiguousMatch_FailsWithSkillNamesInMessage()
    {
        // Two skills both matching csharp — catalog misconfiguration
        var pipeline = PipelineFor("init-project", "csharp",
            new RoleSkillDefinition
            {
                Name = "csharp-bootstrap",
                ActivatesWhen = "pipeline_name = \"init-project\" AND project_language = \"csharp\"",
            },
            new RoleSkillDefinition
            {
                Name = "csharp-bootstrap-alt",
                ActivatesWhen = "pipeline_name = \"init-project\" AND project_language = \"csharp\"",
            });

        var result = await Handler().ExecuteAsync(
            new BootstrapDispatchContext(pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("ambiguous", Exactly.Once());
        result.Message.Should().Contain("csharp-bootstrap");
        result.Message.Should().Contain("csharp-bootstrap-alt");
    }

    [Fact]
    public async Task ExecuteAsync_ActivatesWhenExcludesPipelineName_NoMatch()
    {
        // The csharp-bootstrap skill requires init-project; pipeline_name=fix-bug
        // means the activates_when conjunction fails → no match → fail.
        var pipeline = PipelineFor("fix-bug", "csharp",
            new RoleSkillDefinition
            {
                Name = "csharp-bootstrap",
                ActivatesWhen = "pipeline_name = \"init-project\" AND project_language = \"csharp\"",
            });

        var result = await Handler().ExecuteAsync(
            new BootstrapDispatchContext(pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("no bootstrap skill matched");
    }

    [Fact]
    public async Task ExecuteAsync_NoSkillsLoaded_FailsLoud()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.ResolvedPipeline, new ResolvedPipelineConfig(
            "init-project", new AgentConfig(), "skills/coding", null));

        var result = await Handler().ExecuteAsync(
            new BootstrapDispatchContext(pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("LoadSkills");
    }

    [Fact]
    public async Task ExecuteAsync_MultipleReposDifferentLanguages_EmitsOneRoundPerRepo()
    {
        // p0158g: per-repo dispatch — RepoProjectMaps drives iteration; each
        // repo's PrimaryLanguage selects ONE matching bootstrap skill, and the
        // emitted BootstrapRound carries RepoName so BootstrapRoundHandler can
        // pick the right per-repo sandbox.
        var pipeline = MultiRepoPipelineFor("init-project",
            new[]
            {
                ("server", "csharp"),
                ("client", "typescript"),
                ("docs", "markdown"),
            },
            new RoleSkillDefinition
            {
                Name = "csharp-bootstrap",
                ActivatesWhen = "pipeline_name = \"init-project\" AND project_language = \"csharp\"",
            },
            new RoleSkillDefinition
            {
                Name = "typescript-bootstrap",
                ActivatesWhen = "pipeline_name = \"init-project\" AND project_language = \"typescript\"",
            },
            new RoleSkillDefinition
            {
                Name = "markdown-bootstrap",
                ActivatesWhen = "pipeline_name = \"init-project\" AND project_language = \"markdown\"",
            });

        var result = await Handler().ExecuteAsync(
            new BootstrapDispatchContext(pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.InsertNext.Should().HaveCount(3);
        var byRepo = result.InsertNext.ToDictionary(c => c.RepoName!, c => c.SkillName);
        byRepo["server"].Should().Be("csharp-bootstrap");
        byRepo["client"].Should().Be("typescript-bootstrap");
        byRepo["docs"].Should().Be("markdown-bootstrap");
        result.InsertNext.Should().OnlyContain(c => c.Name == CommandNames.BootstrapRound);
        result.InsertNext.Should().OnlyContain(c => c.Round == 1);
    }

    [Fact]
    public async Task ExecuteAsync_MultiRepo_OneRepoUnmatched_FailsWithRepoNameAndKnownSkills()
    {
        // Two repos; the second has a language with no matching bootstrap skill.
        // The error must name the unmatched repo + the observed slug + the
        // available skills so the operator can pinpoint which repo needs a skill.
        var pipeline = MultiRepoPipelineFor("init-project",
            new[]
            {
                ("server", "csharp"),
                ("mystery", "cobol"),
            },
            new RoleSkillDefinition
            {
                Name = "csharp-bootstrap",
                ActivatesWhen = "pipeline_name = \"init-project\" AND project_language = \"csharp\"",
            },
            new RoleSkillDefinition
            {
                Name = "typescript-bootstrap",
                ActivatesWhen = "pipeline_name = \"init-project\" AND project_language = \"typescript\"",
            });

        var result = await Handler().ExecuteAsync(
            new BootstrapDispatchContext(pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("mystery");
        result.Message.Should().Contain("cobol");
        result.Message.Should().Contain("csharp-bootstrap");
        result.Message.Should().Contain("typescript-bootstrap");
    }

    [Fact]
    public async Task ExecuteAsync_PreservesProjectLanguageConceptAfterIterations()
    {
        // p0158g decision: per-iteration concept mutation is reverted in a
        // finally so the post-dispatch concept state matches the pre-dispatch
        // state. Guards against later steps reading a stale per-repo language.
        var pipeline = MultiRepoPipelineFor("init-project",
            new[]
            {
                ("server", "csharp"),
                ("client", "typescript"),
            },
            new RoleSkillDefinition
            {
                Name = "csharp-bootstrap",
                ActivatesWhen = "pipeline_name = \"init-project\" AND project_language = \"csharp\"",
            },
            new RoleSkillDefinition
            {
                Name = "typescript-bootstrap",
                ActivatesWhen = "pipeline_name = \"init-project\" AND project_language = \"typescript\"",
            });
        // Seed an explicit pre-dispatch concept value so we can detect the revert.
        var concepts = _conceptsFactory(pipeline);
        concepts.SetString("project_language", "scala");

        var result = await Handler().ExecuteAsync(
            new BootstrapDispatchContext(pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        concepts.GetString("project_language").Should().Be("scala");
    }

    [Fact]
    public async Task ExecuteAsync_FiveRepos_QueuesFiveBootstrapRounds()
    {
        // Smoke for the InitProject_FiveRepos_ProducesFivePullRequests done
        // criterion: at the dispatch boundary, 5 repos with 5 distinct
        // languages produce 5 BootstrapRound commands (one per repo), each
        // tagged with the right RepoName. Downstream CommitAndPR (p0158c)
        // already turns one per-repo round into one PR.
        var pipeline = MultiRepoPipelineFor("init-project",
            new[]
            {
                ("api", "csharp"),
                ("web", "typescript"),
                ("docs", "markdown"),
                ("infra", "terraform"),
                ("scripts", "python"),
            },
            new RoleSkillDefinition
            {
                Name = "csharp-bootstrap",
                ActivatesWhen = "pipeline_name = \"init-project\" AND project_language = \"csharp\"",
            },
            new RoleSkillDefinition
            {
                Name = "typescript-bootstrap",
                ActivatesWhen = "pipeline_name = \"init-project\" AND project_language = \"typescript\"",
            },
            new RoleSkillDefinition
            {
                Name = "markdown-bootstrap",
                ActivatesWhen = "pipeline_name = \"init-project\" AND project_language = \"markdown\"",
            },
            new RoleSkillDefinition
            {
                Name = "terraform-bootstrap",
                ActivatesWhen = "pipeline_name = \"init-project\" AND project_language = \"terraform\"",
            },
            new RoleSkillDefinition
            {
                Name = "python-bootstrap",
                ActivatesWhen = "pipeline_name = \"init-project\" AND project_language = \"python\"",
            });

        var result = await Handler().ExecuteAsync(
            new BootstrapDispatchContext(pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.InsertNext.Should().HaveCount(5);
        result.InsertNext.Select(c => c.RepoName).Should()
            .BeEquivalentTo(new[] { "api", "web", "docs", "infra", "scripts" });
    }

    private PipelineContext MultiRepoPipelineFor(
        string pipelineName,
        (string Repo, string Language)[] repos,
        params RoleSkillDefinition[] skills)
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.ResolvedPipeline, new ResolvedPipelineConfig(
            pipelineName, new AgentConfig(), "skills/coding", null));
        pipeline.Set<IReadOnlyList<RoleSkillDefinition>>(ContextKeys.AvailableRoles, skills);
        var maps = repos.ToDictionary(
            r => r.Repo,
            r => new AgentSmith.Domain.Models.ProjectMap(
                PrimaryLanguage: r.Language,
                Frameworks: Array.Empty<string>(),
                Modules: Array.Empty<AgentSmith.Domain.Models.Module>(),
                TestProjects: Array.Empty<AgentSmith.Domain.Models.TestProject>(),
                EntryPoints: Array.Empty<string>(),
                Conventions: new AgentSmith.Domain.Models.Conventions(null, null, null),
                Ci: new AgentSmith.Domain.Models.CiConfig(false, null, null, null)),
            StringComparer.Ordinal);
        pipeline.Set<IReadOnlyDictionary<string, AgentSmith.Domain.Models.ProjectMap>>(
            ContextKeys.RepoProjectMaps, maps);

        var concepts = _conceptsFactory(pipeline);
        concepts.SetEnum("pipeline_name", pipelineName);
        return pipeline;
    }

    private PipelineContext PipelineFor(
        string pipelineName, string projectLanguage, params RoleSkillDefinition[] skills)
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.ResolvedPipeline, new ResolvedPipelineConfig(
            pipelineName, new AgentConfig(), "skills/coding", null));
        pipeline.Set<IReadOnlyList<RoleSkillDefinition>>(ContextKeys.AvailableRoles, skills);
        // p0158g: BootstrapDispatch iterates ProjectMaps (per-repo); single-repo
        // back-compat reads ContextKeys.ProjectMap, so tests seed that with the
        // language slug.
        pipeline.Set(ContextKeys.ProjectMap, new AgentSmith.Domain.Models.ProjectMap(
            PrimaryLanguage: projectLanguage,
            Frameworks: Array.Empty<string>(),
            Modules: Array.Empty<AgentSmith.Domain.Models.Module>(),
            TestProjects: Array.Empty<AgentSmith.Domain.Models.TestProject>(),
            EntryPoints: Array.Empty<string>(),
            Conventions: new AgentSmith.Domain.Models.Conventions(null, null, null),
            Ci: new AgentSmith.Domain.Models.CiConfig(false, null, null, null)));

        var concepts = _conceptsFactory(pipeline);
        concepts.SetEnum("pipeline_name", pipelineName);
        concepts.SetString("project_language", projectLanguage);
        return pipeline;
    }
}
