using AgentSmith.Application.Services.SkillRounds;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Activation;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Decisions;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Skills;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Services.Activation;
using AgentSmith.Tests.Helpers;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.SkillRounds;

public sealed class SkillRoundDispatcherToolSetFlowTests
{
    private static ConceptVocabulary VocabularyWithToolSetSize() =>
        new(new Dictionary<string, ProjectConcept>
        {
            ["tool_set_size"] = new(
                "tool_set_size", "test", ConceptType.Int,
                null, new ConceptIntRange(0, 64), [])
        });

    private static SkillRoundDispatcher BuildDispatcher(
        StubSkillCallRuntime runtime, ConceptVocabulary? vocabulary = null)
    {
        Func<PipelineContext, IRunStateConcepts> conceptsFactory = ctx =>
            new PipelineContextRunStateConcepts(ctx, vocabulary ?? ConceptVocabulary.Empty);
        return new SkillRoundDispatcher(runtime, conceptsFactory, NullLogger<SkillRoundDispatcher>.Instance);
    }

    private static PipelineContext PipelineFor(string pipelineName = "fix-bug")
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.PipelineName, pipelineName);
        pipeline.Set(ContextKeys.AgentConfig, new AgentConfig { Type = "claude" });
        return pipeline;
    }

    private sealed class FixedTools(IReadOnlyList<AITool> tools) : ISkillRoundToolPolicy
    {
        public IReadOnlyList<AITool> GetTools(RoleSkillDefinition role, PipelineContext pipeline) => tools;
    }

    [Fact]
    public async Task SkillRoundDispatcher_PolicyReturnsTools_RuntimeReceivesToolSet()
    {
        var runtime = new StubSkillCallRuntime().ReturnsOk("{}");
        var dispatcher = BuildDispatcher(runtime);
        var pipeline = PipelineFor();
        var role = new RoleSkillDefinition { Name = "s", Role = "investigator", InvestigatorMode = "verify_hint" };
        var fakeTool = AIFunctionFactory.Create((string x) => x, "EchoTool");
        var policy = new FixedTools([fakeTool]);

        await dispatcher.DispatchAsync("s", role, "sys", "prefix", "suffix",
            policy, pipeline, CancellationToken.None);

        runtime.Requests.Should().HaveCount(1);
        runtime.Requests[0].ToolSet.Should().ContainSingle()
            .Which.Should().BeSameAs(fakeTool);
        runtime.Requests[0].InvestigatorMode.Should().Be("verify_hint");
    }

    [Fact]
    public async Task SkillRoundDispatcher_PolicyReturnsEmpty_RuntimeReceivesEmptyToolSet()
    {
        var runtime = new StubSkillCallRuntime().ReturnsOk("{}");
        var dispatcher = BuildDispatcher(runtime);
        var pipeline = PipelineFor();
        var role = new RoleSkillDefinition { Name = "s", Role = "filter" };
        var policy = new FilterRoundToolPolicy();

        await dispatcher.DispatchAsync("s", role, "sys", "prefix", "suffix",
            policy, pipeline, CancellationToken.None);

        runtime.Requests[0].ToolSet.Should().BeEmpty();
    }

    [Fact]
    public async Task SkillRoundDispatcher_DispatchedRound_EmitsToolSetSizeConcept()
    {
        var runtime = new StubSkillCallRuntime().ReturnsOk("{}");
        var vocab = VocabularyWithToolSetSize();
        var dispatcher = BuildDispatcher(runtime, vocab);
        var pipeline = PipelineFor();
        var role = new RoleSkillDefinition { Name = "s", Role = "investigator", InvestigatorMode = "verify_hint" };
        var fakeTool = AIFunctionFactory.Create((string x) => x, "EchoTool");
        var policy = new FixedTools([fakeTool, fakeTool]);

        await dispatcher.DispatchAsync("s", role, "sys", "prefix", "suffix",
            policy, pipeline, CancellationToken.None);

        var concepts = new PipelineContextRunStateConcepts(pipeline, vocab);
        concepts.GetInt("tool_set_size").Should().Be(2);
    }

    [Fact]
    public async Task SkillRoundDispatcher_UndeclaredConcept_DoesNotThrow()
    {
        // Resilience guard: when the deployed skills/concept-vocabulary.yaml
        // doesn't yet declare tool_set_size (cross-repo lag), the dispatch
        // must still complete. SetInt would otherwise throw
        // KeyNotFoundException and abort the round.
        var runtime = new StubSkillCallRuntime().ReturnsOk("{}");
        var dispatcher = BuildDispatcher(runtime, ConceptVocabulary.Empty);
        var pipeline = PipelineFor();
        var role = new RoleSkillDefinition { Name = "s", Role = "filter" };

        var dispatch = async () => await dispatcher.DispatchAsync(
            "s", role, "sys", "prefix", "suffix",
            new FilterRoundToolPolicy(), pipeline, CancellationToken.None);

        await dispatch.Should().NotThrowAsync();
    }
}
