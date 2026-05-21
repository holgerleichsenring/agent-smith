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

    private PipelineContext PipelineFor(
        string pipelineName, string projectLanguage, params RoleSkillDefinition[] skills)
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.ResolvedPipeline, new ResolvedPipelineConfig(
            pipelineName, new AgentConfig(), "skills/coding", null));
        pipeline.Set<IReadOnlyList<RoleSkillDefinition>>(ContextKeys.AvailableRoles, skills);

        var concepts = _conceptsFactory(pipeline);
        concepts.SetEnum("pipeline_name", pipelineName);
        concepts.SetString("project_language", projectLanguage);
        return pipeline;
    }
}
