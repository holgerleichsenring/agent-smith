using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Activation;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Activation;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
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
                OutputSchema = "bootstrap",
            },
            new RoleSkillDefinition
            {
                Name = "node-bootstrap",
                ActivatesWhen = "pipeline_name = \"init-project\" AND project_language = \"node\"",
                OutputSchema = "bootstrap",
            });

        var result = await Handler().ExecuteAsync(
            new BootstrapDispatchContext(pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.InsertNext.Should().HaveCount(1);
        var emitted = result.InsertNext[0];
        emitted.Name.Should().Be(CommandNames.BootstrapRound);
        emitted.SkillName.Should().Be("csharp-bootstrap");
        emitted.Round.Should().Be(1);
        // p0161d: each emitted round carries ContextName + Workdir
        emitted.ContextName.Should().Be("default");
        emitted.Workdir.Should().Be(".");
    }

    [Fact]
    public async Task ExecuteAsync_NoMatch_FailMessage_IncludesObservedLanguageAndAvailableSkillNames()
    {
        var pipeline = PipelineFor("init-project", "python",
            new RoleSkillDefinition
            {
                Name = "node-bootstrap",
                ActivatesWhen = "pipeline_name = \"init-project\" AND project_language = \"node\"",
                OutputSchema = "bootstrap",
            },
            new RoleSkillDefinition
            {
                Name = "generic-bootstrap",
                ActivatesWhen = "pipeline_name = \"init-project\" AND project_language = \"generic\"",
                OutputSchema = "bootstrap",
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
        var pipeline = PipelineFor("init-project", "csharp",
            new RoleSkillDefinition
            {
                Name = "csharp-bootstrap",
                ActivatesWhen = "pipeline_name = \"init-project\" AND project_language = \"csharp\"",
                OutputSchema = "bootstrap",
            },
            new RoleSkillDefinition
            {
                Name = "csharp-bootstrap-alt",
                ActivatesWhen = "pipeline_name = \"init-project\" AND project_language = \"csharp\"",
                OutputSchema = "bootstrap",
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
        var pipeline = PipelineFor("fix-bug", "csharp",
            new RoleSkillDefinition
            {
                Name = "csharp-bootstrap",
                ActivatesWhen = "pipeline_name = \"init-project\" AND project_language = \"csharp\"",
                OutputSchema = "bootstrap",
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
                OutputSchema = "bootstrap",
            },
            new RoleSkillDefinition
            {
                Name = "typescript-bootstrap",
                ActivatesWhen = "pipeline_name = \"init-project\" AND project_language = \"typescript\"",
                OutputSchema = "bootstrap",
            },
            new RoleSkillDefinition
            {
                Name = "markdown-bootstrap",
                ActivatesWhen = "pipeline_name = \"init-project\" AND project_language = \"markdown\"",
                OutputSchema = "bootstrap",
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
                OutputSchema = "bootstrap",
            },
            new RoleSkillDefinition
            {
                Name = "typescript-bootstrap",
                ActivatesWhen = "pipeline_name = \"init-project\" AND project_language = \"typescript\"",
                OutputSchema = "bootstrap",
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
                OutputSchema = "bootstrap",
            },
            new RoleSkillDefinition
            {
                Name = "typescript-bootstrap",
                ActivatesWhen = "pipeline_name = \"init-project\" AND project_language = \"typescript\"",
                OutputSchema = "bootstrap",
            });
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
                OutputSchema = "bootstrap",
            },
            new RoleSkillDefinition
            {
                Name = "typescript-bootstrap",
                ActivatesWhen = "pipeline_name = \"init-project\" AND project_language = \"typescript\"",
                OutputSchema = "bootstrap",
            },
            new RoleSkillDefinition
            {
                Name = "markdown-bootstrap",
                ActivatesWhen = "pipeline_name = \"init-project\" AND project_language = \"markdown\"",
                OutputSchema = "bootstrap",
            },
            new RoleSkillDefinition
            {
                Name = "terraform-bootstrap",
                ActivatesWhen = "pipeline_name = \"init-project\" AND project_language = \"terraform\"",
                OutputSchema = "bootstrap",
            },
            new RoleSkillDefinition
            {
                Name = "python-bootstrap",
                ActivatesWhen = "pipeline_name = \"init-project\" AND project_language = \"python\"",
                OutputSchema = "bootstrap",
            });

        var result = await Handler().ExecuteAsync(
            new BootstrapDispatchContext(pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.InsertNext.Should().HaveCount(5);
        result.InsertNext.Select(c => c.RepoName).Should()
            .BeEquivalentTo(new[] { "api", "web", "docs", "infra", "scripts" });
    }

    [Fact]
    public async Task ExecuteAsync_AmbiguityFromDiscover_RefusesToEmitRounds()
    {
        // p0161d: when BootstrapDiscoverHandler marked discovery ambiguous,
        // BootstrapDispatchHandler must refuse to emit any round and propagate
        // the structured fail-loud message so the operator re-runs via CLI.
        var pipeline = PipelineFor("init-project", "csharp",
            new RoleSkillDefinition
            {
                Name = "csharp-bootstrap",
                ActivatesWhen = "pipeline_name = \"init-project\" AND project_language = \"csharp\"",
                OutputSchema = "bootstrap",
            });
        pipeline.Set(ContextKeys.DiscoveryAmbiguous,
            "BootstrapDiscover: repo 'api' is ambiguous — Server vs API. Candidates: [Server, API]. Re-run via CLI.");

        var result = await Handler().ExecuteAsync(
            new BootstrapDispatchContext(pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("ambiguous");
        result.Message.Should().Contain("Re-run via CLI");
        result.InsertNext.Should().BeNullOrEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_MultipleContexts_EmitsOneRoundPerContext()
    {
        // p0161d: monorepo with N components → N BootstrapRound commands per repo,
        // each carrying ContextName + Workdir + language-matched skill.
        var pipeline = MultiContextPipelineFor("init-project", "monorepo",
            new[]
            {
                ("server", ".", "csharp", "src/server/Program.cs"),
                ("client", "client", "typescript", "client/package.json"),
                ("docs", "docs", "markdown", "docs/index.md"),
            },
            new RoleSkillDefinition
            {
                Name = "csharp-bootstrap",
                ActivatesWhen = "pipeline_name = \"init-project\" AND project_language = \"csharp\"",
                OutputSchema = "bootstrap",
            },
            new RoleSkillDefinition
            {
                Name = "typescript-bootstrap",
                ActivatesWhen = "pipeline_name = \"init-project\" AND project_language = \"typescript\"",
                OutputSchema = "bootstrap",
            },
            new RoleSkillDefinition
            {
                Name = "markdown-bootstrap",
                ActivatesWhen = "pipeline_name = \"init-project\" AND project_language = \"markdown\"",
                OutputSchema = "bootstrap",
            });

        var result = await Handler().ExecuteAsync(
            new BootstrapDispatchContext(pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.InsertNext.Should().HaveCount(3);
        var byContext = result.InsertNext.ToDictionary(c => c.ContextName!, c => c);
        byContext["server"].Workdir.Should().Be(".");
        byContext["server"].SkillName.Should().Be("csharp-bootstrap");
        byContext["client"].Workdir.Should().Be("client");
        byContext["client"].SkillName.Should().Be("typescript-bootstrap");
        byContext["docs"].Workdir.Should().Be("docs");
        byContext["docs"].SkillName.Should().Be("markdown-bootstrap");
        result.InsertNext.Should().OnlyContain(c => c.RepoName == "monorepo");
    }

    [Fact]
    public async Task ExecuteAsync_SingleDefaultContext_OneRound()
    {
        // p0161d: legitimately single-component repo gets exactly one
        // BootstrapRound with ContextName="default", Workdir=".".
        var pipeline = PipelineFor("init-project", "csharp",
            new RoleSkillDefinition
            {
                Name = "csharp-bootstrap",
                ActivatesWhen = "pipeline_name = \"init-project\" AND project_language = \"csharp\"",
                OutputSchema = "bootstrap",
            });

        var result = await Handler().ExecuteAsync(
            new BootstrapDispatchContext(pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.InsertNext.Should().HaveCount(1);
        result.InsertNext[0].ContextName.Should().Be("default");
        result.InsertNext[0].Workdir.Should().Be(".");
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
        // p0161d: seed DiscoveredComponents with one synthetic-default entry per repo.
        var components = repos.ToDictionary(
            r => r.Repo,
            r => (IReadOnlyList<DiscoveredComponent>)
                new[] { new DiscoveredComponent("default", ".", r.Language, $"{r.Repo}/.agentsmith/context.yaml") },
            StringComparer.Ordinal);
        pipeline.Set<IReadOnlyDictionary<string, IReadOnlyList<DiscoveredComponent>>>(
            ContextKeys.DiscoveredComponents, components);

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
        // p0161d: dispatch reads DiscoveredComponents — seed one synthetic default.
        var components = new Dictionary<string, IReadOnlyList<DiscoveredComponent>>(StringComparer.Ordinal)
        {
            ["primary"] = new[]
            {
                new DiscoveredComponent("default", ".", projectLanguage, ".agentsmith/context.yaml"),
            },
        };
        pipeline.Set<IReadOnlyDictionary<string, IReadOnlyList<DiscoveredComponent>>>(
            ContextKeys.DiscoveredComponents, components);

        var concepts = _conceptsFactory(pipeline);
        concepts.SetEnum("pipeline_name", pipelineName);
        concepts.SetString("project_language", projectLanguage);
        return pipeline;
    }

    private PipelineContext MultiContextPipelineFor(
        string pipelineName, string repoName,
        (string ContextName, string Workdir, string Language, string Evidence)[] contexts,
        params RoleSkillDefinition[] skills)
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.ResolvedPipeline, new ResolvedPipelineConfig(
            pipelineName, new AgentConfig(), "skills/coding", null));
        pipeline.Set<IReadOnlyList<RoleSkillDefinition>>(ContextKeys.AvailableRoles, skills);
        var components = new Dictionary<string, IReadOnlyList<DiscoveredComponent>>(StringComparer.Ordinal)
        {
            [repoName] = contexts.Select(c =>
                new DiscoveredComponent(c.ContextName, c.Workdir, c.Language, c.Evidence)).ToList(),
        };
        pipeline.Set<IReadOnlyDictionary<string, IReadOnlyList<DiscoveredComponent>>>(
            ContextKeys.DiscoveredComponents, components);

        var concepts = _conceptsFactory(pipeline);
        concepts.SetEnum("pipeline_name", pipelineName);
        return pipeline;
    }
}
