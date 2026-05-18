using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Activation;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Activation;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Skills;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Handlers;

public sealed class VerifyRoundHandlerTests
{
    [Fact]
    public async Task ExecuteAsync_NoVerifiersInRoles_ReturnsOkSkippedMessage()
    {
        var pipeline = PipelineWithPlanAndDiff(roles: new RoleSkillDefinition[]
        {
            JudgeOnly("backend-developer-judge"),
        });

        var result = await BuildSut("[]").ExecuteAsync(
            new RunVerifyPhaseContext(new AgentConfig(), pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("no active verifiers");
    }

    [Fact]
    public async Task ExecuteAsync_VerifierEmitsNoBlocking_ReturnsOk_NoInsertNext()
    {
        var pipeline = PipelineWithPlanAndDiff(roles: new[] { ScopeVerifierRole() });

        var result = await BuildSut("[]").ExecuteAsync(
            new RunVerifyPhaseContext(new AgentConfig(), pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.InsertNext.Should().BeNull();
        pipeline.TryGet<int>(ContextKeys.VerifyRoundCount, out var rc).Should().BeTrue();
        rc.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_VerifierEmitsBlockingFirstRound_InsertsAgenticExecuteAndRunVerify()
    {
        var pipeline = PipelineWithPlanAndDiff(roles: new[] { ScopeVerifierRole() });
        var blockingObs = """
            [{"role":"scope-verifier","concern":"Correctness","description":"x.cs out of scope",
              "suggestion":"add to plan","blocking":true,"severity":"high","confidence":85,
              "file":"x.cs"}]
            """;

        var result = await BuildSut(blockingObs).ExecuteAsync(
            new RunVerifyPhaseContext(new AgentConfig(), pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.InsertNext.Should().NotBeNull();
        result.InsertNext!.Should().HaveCount(2);
        result.InsertNext![0].Name.Should().Be(CommandNames.AgenticExecute);
        result.InsertNext![1].Name.Should().Be(CommandNames.RunVerifyPhase);
    }

    [Fact]
    public async Task ExecuteAsync_VerifierEmitsBlockingFirstRound_SetsVerifyNotesInContext()
    {
        var pipeline = PipelineWithPlanAndDiff(roles: new[] { ScopeVerifierRole() });
        var blockingObs = """
            [{"role":"scope-verifier","concern":"Correctness","description":"x.cs out of scope",
              "suggestion":"add to plan","blocking":true,"severity":"high","confidence":85,
              "file":"x.cs"}]
            """;

        await BuildSut(blockingObs).ExecuteAsync(
            new RunVerifyPhaseContext(new AgentConfig(), pipeline), CancellationToken.None);

        pipeline.TryGet<string>(ContextKeys.VerifyNotes, out var notes).Should().BeTrue();
        notes.Should().Contain("Verify round 1");
        notes.Should().Contain("x.cs out of scope");
    }

    [Fact]
    public async Task ExecuteAsync_BlockingSecondRound_ReturnsFailWithDedupedCombinedNotes()
    {
        // p0129c: Escalate dedups across rounds via VerifyObservations. Seed round-1
        // observations as if AppendObservations had run; round-2 emits a duplicate +
        // a new observation; combined output should have two unique entries.
        var pipeline = PipelineWithPlanAndDiff(roles: new[] { ScopeVerifierRole() });
        pipeline.Set(ContextKeys.VerifyRoundCount, 1);
        pipeline.Set(ContextKeys.VerifyObservations, new List<SkillObservation>
        {
            new(0, "scope-verifier", ObservationConcern.Correctness,
                "still out", "fix", true, ObservationSeverity.High, 90, File: "x.cs"),
        });
        var round2 = """
            [
              {"role":"scope-verifier","concern":"Correctness","description":"still out","suggestion":"fix","blocking":true,"severity":"high","confidence":90,"file":"x.cs"},
              {"role":"scope-verifier","concern":"Correctness","description":"y.cs new","suggestion":"add","blocking":true,"severity":"high","confidence":85,"file":"y.cs"}
            ]
            """;

        var result = await BuildSut(round2).ExecuteAsync(
            new RunVerifyPhaseContext(new AgentConfig(), pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("escalation");
        pipeline.TryGet<string>(ContextKeys.VerifyNotes, out var combined).Should().BeTrue();
        combined.Should().Contain("Verify round 2");
        combined.Should().Contain("still out");
        combined.Should().Contain("y.cs new");
        // Dedup: 'still out' appears exactly once even though round 1 + round 2 both emitted it.
        var occurrences = combined!.Split("still out").Length - 1;
        occurrences.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_VerifyRoundCountIncrementsToTwoOnSecondInvocation()
    {
        var pipeline = PipelineWithPlanAndDiff(roles: new[] { ScopeVerifierRole() });
        var sut = BuildSut("[]");

        await sut.ExecuteAsync(new RunVerifyPhaseContext(new AgentConfig(), pipeline), CancellationToken.None);
        await sut.ExecuteAsync(new RunVerifyPhaseContext(new AgentConfig(), pipeline), CancellationToken.None);

        pipeline.TryGet<int>(ContextKeys.VerifyRoundCount, out var rc).Should().BeTrue();
        rc.Should().Be(2);
    }

    [Fact]
    public async Task ExecuteAsync_NoPlanAndNoDiff_ReturnsOkSkippedMessage()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.AvailableRoles,
            (IReadOnlyList<RoleSkillDefinition>)new[] { ScopeVerifierRole() });

        var result = await BuildSut("[]").ExecuteAsync(
            new RunVerifyPhaseContext(new AgentConfig(), pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("skipped");
    }

    [Fact]
    public async Task ExecuteAsync_NonBlockingObservation_AppendedToVerifyObservations()
    {
        var pipeline = PipelineWithPlanAndDiff(roles: new[] { ScopeVerifierRole() });
        var nonBlocking = """
            [{"role":"scope-verifier","concern":"Correctness","description":"minor",
              "suggestion":"info","blocking":false,"severity":"low","confidence":60}]
            """;

        await BuildSut(nonBlocking).ExecuteAsync(
            new RunVerifyPhaseContext(new AgentConfig(), pipeline), CancellationToken.None);

        pipeline.TryGet<List<SkillObservation>>(ContextKeys.VerifyObservations, out var obs).Should().BeTrue();
        obs!.Should().HaveCount(1);
        obs![0].Description.Should().Be("minor");
    }

    [Fact]
    public async Task ExecuteAsync_BlockingObservationWithLowConfidence_DowngradedAndReturnsOk()
    {
        var pipeline = PipelineWithPlanAndDiff(roles: new[] { ScopeVerifierRole() });
        // Confidence < 70 + Blocking → downgraded by ApplyConfidenceThreshold.
        var lowConfBlocking = """
            [{"role":"scope-verifier","concern":"Correctness","description":"x.cs unsure",
              "suggestion":"check","blocking":true,"severity":"medium","confidence":60}]
            """;

        var result = await BuildSut(lowConfBlocking).ExecuteAsync(
            new RunVerifyPhaseContext(new AgentConfig(), pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.InsertNext.Should().BeNull();
        pipeline.TryGet<List<SkillObservation>>(ContextKeys.VerifyObservations, out var obs);
        obs![0].Blocking.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_FilterExcludesNonMatchingActivatesWhen_VerifierSkipped()
    {
        // Verifier activates only on api-security-scan; pipeline_name is fix-bug → excluded.
        var apiOnly = ScopeVerifierRole();
        apiOnly.ActivatesWhen = "pipeline_name = \"api-security-scan\"";
        var pipeline = PipelineWithPlanAndDiff(roles: new[] { apiOnly });

        var result = await BuildSut("WOULD_NEVER_PARSE").ExecuteAsync(
            new RunVerifyPhaseContext(new AgentConfig(), pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("no active verifiers");
    }

    private static VerifyRoundHandler BuildSut(string chatResponseJson)
    {
        var stubChat = new StubChatClient(new Queue<string>(new[] { chatResponseJson }));
        var stubFactory = new StubChatClientFactory(stubChat);
        var tokenizer = new ActivationExpressionTokenizer();
        var parser = new ActivationExpressionParser(tokenizer);
        var evaluator = new ActivationEvaluator();
        var filter = new ActivationSkillFilter(parser, evaluator, NullLogger<ActivationSkillFilter>.Instance);
        var bodyResolver = new Mock<ISkillBodyResolver>();
        bodyResolver
            .Setup(b => b.ResolveBody(It.IsAny<RoleSkillDefinition>(), It.IsAny<SkillRole>()))
            .Returns("Verifier body — flag any out-of-scope file.");
        var runtime = BuildRuntime(stubFactory);
        return new VerifyRoundHandler(
            filter, bodyResolver.Object,
            RunStateConceptsTestFactory.Default,
            Mock.Of<AgentSmith.Contracts.Decisions.IDecisionLogger>(),
            new AgentSmith.Application.Services.Tools.ToolKit(
                new AgentSmith.Application.Services.Tools.AllHostsActivePolicy()),
            runtime,
            NullLogger<VerifyRoundHandler>.Instance);
    }

    private static AgentSmith.Application.Services.Loop.SkillCallRuntime BuildRuntime(
        StubChatClientFactory factory)
    {
        var limits = new LoopLimitsConfig();
        var noOp = new AgentSmith.Application.Services.Loop.NoOpSkillOutputValidator();
        var validatorFactory = new AgentSmith.Application.Services.Validation.SkillOutputValidatorFactory(noOp, noOp);
        return new AgentSmith.Application.Services.Loop.SkillCallRuntime(
            factory,
            new AgentSmith.Application.Services.Loop.PipelineConcurrencyGate(limits),
            limits,
            new AgentSmith.Application.Services.Loop.OutcomeClassifier(),
            new AgentSmith.Application.Services.Loop.RetryCoordinator(),
            validatorFactory,
            NullLogger<AgentSmith.Application.Services.Loop.SkillCallRuntime>.Instance);
    }

    private static PipelineContext PipelineWithPlanAndDiff(IReadOnlyList<RoleSkillDefinition> roles)
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.ResolvedPipeline, new ResolvedPipelineConfig(
            "fix-bug", new AgentConfig(), "skills/coding", null));
        pipeline.Set(ContextKeys.PlanJson,
            """{"summary":"fix it","scope":{"files":["a.cs"]},"steps":[],"status":"complete"}""");
        pipeline.Set(ContextKeys.DiffJson,
            """{"changes":[{"file":"a.cs","operation":"modify"},{"file":"x.cs","operation":"modify"}]}""");
        pipeline.Set(ContextKeys.AvailableRoles, roles);
        // Publish pipeline_name so activates_when expressions evaluate against a real value
        // (otherwise the unset-concept default — first enum value 'api-security-scan' —
        // would let api-only verifiers spuriously match).
        var concepts = RunStateConceptsTestFactory.Default(pipeline);
        concepts.SetEnum("pipeline_name", "fix-bug");
        return pipeline;
    }

    private static RoleSkillDefinition ScopeVerifierRole() => new()
    {
        Name = "scope-verifier",
        DisplayName = "scope-verifier",
        Rules = "body",
        Role = "investigator",
        InvestigatorMode = "verify_diff",
        OutputSchema = "observation",
        ActivatesWhen = "pipeline_name = \"fix-bug\" OR pipeline_name = \"feature-implementation\"",
    };

    private static RoleSkillDefinition JudgeOnly(string name) => new()
    {
        Name = name,
        DisplayName = name,
        Rules = "body",
        Role = "judge",
        OutputSchema = "observation",
    };
}
