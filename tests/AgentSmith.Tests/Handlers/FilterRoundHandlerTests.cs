using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Skills;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Handlers;

public sealed class FilterRoundHandlerTests
{
    [Fact]
    public async Task ExecuteAsync_OutputList_ReducesObservations()
    {
        var responseJson = """
            [{"role":"reducer","concern":"Authentication","description":"Auth check missing","suggestion":"add guard","blocking":true,"severity":"High","confidence":85}]
            """;
        var (handler, pipeline) = BuildHandlerAndPipeline(
            "reducer", OutputForm.List, responseJson,
            initialObservations: new List<SkillObservation>
            {
                Obs(1, "Concern A"),
                Obs(2, "Concern B"),
                Obs(3, "Concern C"),
            });
        var context = new FilterRoundContext("reducer", 3, AgentForTests(), pipeline);

        var result = await handler.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var stored = pipeline.Get<List<SkillObservation>>(ContextKeys.SkillObservations);
        stored.Should().HaveCount(1);
        stored[0].Id.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_OutputArtifact_ProducesArtifact()
    {
        const string artifactText = "Final report. All findings reviewed and synthesized.";
        var (handler, pipeline) = BuildHandlerAndPipeline(
            "synth", OutputForm.Artifact, artifactText,
            initialObservations: new List<SkillObservation> { Obs(1, "Some finding") });
        var context = new FilterRoundContext("synth", 3, AgentForTests(), pipeline);

        var result = await handler.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var outputs = pipeline.Get<Dictionary<string, string>>(ContextKeys.SkillOutputs);
        outputs.Should().ContainKey("synth");
        outputs["synth"].Should().Be(artifactText);
    }

    [Fact]
    public async Task ExecuteAsync_SkillNotInAvailableRoles_FailsLoud()
    {
        var (handler, pipeline) = BuildHandlerAndPipeline(
            "registered", OutputForm.List, "[]", new List<SkillObservation>());
        var context = new FilterRoundContext("not-registered", 3, AgentForTests(), pipeline);

        var result = await handler.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("not-registered");
    }

    private static (FilterRoundHandler handler, PipelineContext pipeline) BuildHandlerAndPipeline(
        string skillName, OutputForm filterForm, string llmResponse, List<SkillObservation> initialObservations)
    {
        var llmClientMock = new Mock<ILlmClient>();
        llmClientMock.Setup(c => c.CompleteAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<TaskType>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse(llmResponse, 0, 0));

        var llmFactoryMock = new Mock<ILlmClientFactory>();
        llmFactoryMock.Setup(f => f.Create(It.IsAny<AgentConfig>())).Returns(llmClientMock.Object);

        var promptBuilderMock = new Mock<ISkillPromptBuilder>();
        promptBuilderMock.Setup(p => p.BuildStructuredPromptParts(
                It.IsAny<RoleSkillDefinition>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string?>(), It.IsAny<SkillRole?>(), It.IsAny<PlanArtifact?>()))
            .Returns(("system", "prefix", "suffix"));

        var handler = new FilterRoundHandler(
            llmFactoryMock.Object, promptBuilderMock.Object,
            NullLogger<FilterRoundHandler>.Instance);

        var pipeline = new PipelineContext();
        pipeline.Set<IReadOnlyList<RoleSkillDefinition>>(
            ContextKeys.AvailableRoles,
            new[]
            {
                new RoleSkillDefinition
                {
                    Name = skillName,
                    DisplayName = skillName,
                    Description = $"Skill {skillName}",
                    Rules = "filter rules",
                    OutputContract = new OutputContract(
                        SchemaRef: string.Empty,
                        MaxObservations: 10, MaxCharsPerField: 500,
                        OutputType: new Dictionary<SkillRole, OutputForm> { [SkillRole.Filter] = filterForm })
                }
            });
        pipeline.Set(ContextKeys.SkillObservations, initialObservations);
        pipeline.Set(ContextKeys.ResolvedPipeline,
            new ResolvedPipelineConfig("test-pipeline", AgentForTests(), "skills/coding", null));

        return (handler, pipeline);
    }

    private static SkillObservation Obs(int id, string concern) =>
        new(id, "filter", ObservationConcern.Risk, concern, "fix it", false, ObservationSeverity.Medium, 80);

    private static AgentConfig AgentForTests() => new() { Type = "claude" };
}
