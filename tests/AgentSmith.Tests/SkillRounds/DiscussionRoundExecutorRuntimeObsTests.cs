using AgentSmith.Application.Models;
using AgentSmith.Application.Services;
using AgentSmith.Application.Services.SkillRounds;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Skills;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.SkillRounds;

/// <summary>
/// Regression: when SkillCallRuntime short-circuits a call (cost cap exhausted,
/// execution-limit hit) the result carries Output=null + a typed
/// RuntimeObservation explaining the reason. Before the fix DiscussionRoundExecutor
/// dropped the RuntimeObservations and ran the parser on the empty string,
/// producing a ghost INFO observation with empty Description. The fix appends
/// RuntimeObservations to the bus and skips parsing when Output is whitespace.
/// </summary>
public sealed class DiscussionRoundExecutorRuntimeObsTests
{
    [Fact]
    public async Task CostCapExhausted_SurfacesRuntimeObservation_NotGhostInfo()
    {
        var costCapObs = new SkillObservation(
            Id: 0, Role: "report-synthesizer",
            Concern: ObservationConcern.Correctness,
            Description: "Skill report-synthesizer skipped — pipeline cost cap exhausted (1.9065 USD / 931058 tokens).",
            Suggestion: "", Blocking: false,
            Severity: ObservationSeverity.Info, Confidence: 100,
            Category: ExecutionLimitCategories.CostCapExhausted);

        var dispatcher = MockDispatcher(new SkillCallResult
        {
            Outcome = SkillCallOutcome.Incomplete,
            Output = null,
            Cost = MakeCost("report-synthesizer", "judge"),
            Trace = Array.Empty<LoopTraceEntry>(),
            FailureReason = "cost cap exhausted",
            RuntimeObservations = new[] { costCapObs },
            ReadPaths = Array.Empty<string>(),
        });

        var (sut, bufferSpy) = BuildExecutor(dispatcher);
        await sut.ExecuteAsync(
            "report-synthesizer", MakeRole("report-synthesizer"), Array.Empty<RoleSkillDefinition>(),
            round: 2, MockStrategy(), MockToolPolicy(), new PipelineContext(),
            NullLogger.Instance, CancellationToken.None);

        bufferSpy.Buffer.Should().NotBeNull();
        bufferSpy.Buffer!.Observations.Should().HaveCount(1);
        bufferSpy.Buffer.Observations[0].Category.Should().Be(ExecutionLimitCategories.CostCapExhausted);
        bufferSpy.Buffer.Observations[0].Description.Should().Contain("cost cap exhausted");
    }

    [Fact]
    public async Task EmptyOutput_NoRuntimeObs_ProducesZeroObservations_NoGhostFallback()
    {
        var dispatcher = MockDispatcher(new SkillCallResult
        {
            Outcome = SkillCallOutcome.Ok,
            Output = "",
            Cost = MakeCost("some-judge", "judge"),
            Trace = Array.Empty<LoopTraceEntry>(),
            RuntimeObservations = Array.Empty<SkillObservation>(),
            ReadPaths = Array.Empty<string>(),
        });

        var (sut, bufferSpy) = BuildExecutor(dispatcher);
        await sut.ExecuteAsync(
            "some-judge", MakeRole("some-judge"), Array.Empty<RoleSkillDefinition>(),
            round: 1, MockStrategy(), MockToolPolicy(), new PipelineContext(),
            NullLogger.Instance, CancellationToken.None);

        bufferSpy.Buffer!.Observations.Should().BeEmpty();
    }

    [Fact]
    public async Task ParsedOutput_PlusRuntimeObs_MergesBoth()
    {
        var execLimitObs = new SkillObservation(
            Id: 0, Role: "auth-tester",
            Concern: ObservationConcern.Correctness,
            Description: "Skill auth-tester hit per-skill tool-call cap",
            Suggestion: "", Blocking: false,
            Severity: ObservationSeverity.Info, Confidence: 100,
            Category: ExecutionLimitCategories.ExecutionLimitToolCalls);

        var dispatcher = MockDispatcher(new SkillCallResult
        {
            Outcome = SkillCallOutcome.Ok,
            Output = "[{\"concern\":\"security\",\"description\":\"Real finding\",\"severity\":\"high\",\"confidence\":80,\"blocking\":false}]",
            Cost = MakeCost("auth-tester", "investigator"),
            Trace = Array.Empty<LoopTraceEntry>(),
            RuntimeObservations = new[] { execLimitObs },
            ReadPaths = Array.Empty<string>(),
        });

        var (sut, bufferSpy) = BuildExecutor(dispatcher);
        await sut.ExecuteAsync(
            "auth-tester", MakeRole("auth-tester"), Array.Empty<RoleSkillDefinition>(),
            round: 1, MockStrategy(), MockToolPolicy(), new PipelineContext(),
            NullLogger.Instance, CancellationToken.None);

        bufferSpy.Buffer!.Observations.Should().HaveCount(2);
        bufferSpy.Buffer.Observations.Should().Contain(o => o.Description.Contains("Real finding"));
        bufferSpy.Buffer.Observations.Should().Contain(o => o.Category == ExecutionLimitCategories.ExecutionLimitToolCalls);
    }

    private static Mock<ISkillRoundDispatcher> MockDispatcher(SkillCallResult result)
    {
        var mock = new Mock<ISkillRoundDispatcher>();
        mock.Setup(d => d.DispatchAsync(
                It.IsAny<string>(), It.IsAny<RoleSkillDefinition>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<ISkillRoundToolPolicy>(), It.IsAny<PipelineContext>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);
        return mock;
    }

    private static (DiscussionRoundExecutor sut, BufferSpy spy) BuildExecutor(Mock<ISkillRoundDispatcher> dispatcher)
    {
        var composer = new Mock<IPromptComposer>();
        composer.Setup(c => c.ComposeDiscussion(
                It.IsAny<RoleSkillDefinition>(), It.IsAny<ISkillPromptStrategy>(),
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<PipelineContext>()))
            .Returns(("sys", "userPrefix", "userSuffix"));

        var responseParser = new Mock<ISkillResponseParser>();
        responseParser.Setup(p => p.ParseAndDowngrade(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<Microsoft.Extensions.Logging.ILogger>(), It.IsAny<IReadOnlyCollection<string>>()))
            .Returns<string, string, Microsoft.Extensions.Logging.ILogger, IReadOnlyCollection<string>>((text, _, _, _) =>
                string.IsNullOrWhiteSpace(text)
                    ? new List<SkillObservation>()
                    : new List<SkillObservation>
                    {
                        new(Id: 1, Role: "test",
                            Concern: ObservationConcern.Security,
                            Description: "Real finding",
                            Suggestion: "", Blocking: false,
                            Severity: ObservationSeverity.High, Confidence: 80),
                    });
        responseParser.Setup(p => p.RenderObservationsAsText(It.IsAny<IReadOnlyList<SkillObservation>>()))
            .Returns("rendered");

        var bufferSpy = new BufferSpy();
        var bufferDispatcher = new Mock<ISkillRoundBufferDispatcher>();
        bufferDispatcher.Setup(d => d.Dispatch(It.IsAny<PipelineContext>(), It.IsAny<SkillRoundBuffer>()))
            .Callback<PipelineContext, SkillRoundBuffer>((_, buf) => bufferSpy.Buffer = buf);

        var detector = new Mock<IBlockingFollowUpDetector>();
        detector.Setup(d => d.Detect(
                It.IsAny<IReadOnlyList<SkillObservation>>(), It.IsAny<string>(),
                It.IsAny<RoleSkillDefinition>(), It.IsAny<IReadOnlyList<RoleSkillDefinition>>(),
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<PipelineContext>(),
                It.IsAny<Microsoft.Extensions.Logging.ILogger>()))
            .Returns((CommandResult?)null);

        var sut = new DiscussionRoundExecutor(
            composer.Object, dispatcher.Object, responseParser.Object,
            bufferDispatcher.Object, detector.Object);
        return (sut, bufferSpy);
    }

    private static RoleSkillDefinition MakeRole(string name) =>
        new()
        {
            Name = name,
            DisplayName = name,
            Emoji = "🔎",
            Role = "judge",
            Description = "test",
            OutputSchema = "observation",
        };

    private static CallCostRecord MakeCost(string skillName, string role) =>
        new()
        {
            SkillName = skillName,
            Role = role,
            Phase = SkillExecutionPhase.Discuss,
            StartedAt = DateTimeOffset.UtcNow,
        };

    private static ISkillPromptStrategy MockStrategy()
    {
        var mock = new Mock<ISkillPromptStrategy>();
        mock.SetupGet(s => s.SkillRoundCommandName).Returns("TestSkillRoundCommand");
        return mock.Object;
    }

    private static ISkillRoundToolPolicy MockToolPolicy() => new Mock<ISkillRoundToolPolicy>().Object;

    private sealed class BufferSpy
    {
        public SkillRoundBuffer? Buffer { get; set; }
    }
}
