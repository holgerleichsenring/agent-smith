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

/// <summary>
/// p0129b: VerifyRoundHandler dispatches all three coding verifiers (scope, build,
/// test) when active and aggregates their observations. Smoke for the verifier-set
/// growth from p0129a → p0129b without changes to the handler itself.
/// </summary>
public sealed class VerifyRoundHandlerMultiVerifierTests
{
    [Fact]
    public async Task ExecuteAsync_ThreeActiveVerifiers_AllInvokedAndObservationsAggregated()
    {
        var pipeline = PipelineFor(new[]
        {
            VerifierRole("scope-verifier"),
            VerifierRole("build-verifier"),
            VerifierRole("test-verifier"),
        });
        var responses = new Queue<string>(new[]
        {
            """[{"role":"scope-verifier","concern":"Correctness","description":"x.cs out","suggestion":"add","blocking":false,"severity":"low","confidence":60}]""",
            """[{"role":"build-verifier","concern":"Correctness","description":"missing using","suggestion":"add import","blocking":false,"severity":"medium","confidence":65}]""",
            """[{"role":"test-verifier","concern":"Correctness","description":"new method without tests","suggestion":"add test","blocking":false,"severity":"medium","confidence":62}]""",
        });

        var sut = BuildSut(responses);
        var result = await sut.ExecuteAsync(
            new RunVerifyPhaseContext(new AgentConfig(), pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        pipeline.TryGet<List<SkillObservation>>(ContextKeys.VerifyObservations, out var obs).Should().BeTrue();
        obs!.Should().HaveCount(3);
        obs!.Select(o => o.Description).Should().Contain(new[] { "x.cs out", "missing using", "new method without tests" });
    }

    [Fact]
    public async Task ExecuteAsync_OneBlockingFromBuildVerifier_TriggersReLoop()
    {
        var pipeline = PipelineFor(new[]
        {
            VerifierRole("scope-verifier"),
            VerifierRole("build-verifier"),
            VerifierRole("test-verifier"),
        });
        var responses = new Queue<string>(new[]
        {
            """[]""", // scope-verifier: no observations
            """[{"role":"build-verifier","concern":"Correctness","description":"removed-but-still-referenced","suggestion":"add back","blocking":true,"severity":"high","confidence":85,"file":"a.cs"}]""",
            """[]""", // test-verifier: no observations
        });

        var result = await BuildSut(responses).ExecuteAsync(
            new RunVerifyPhaseContext(new AgentConfig(), pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.InsertNext.Should().NotBeNull();
        result.InsertNext!.Should().HaveCount(2);
        result.InsertNext![0].Name.Should().Be(CommandNames.AgenticExecute);
        result.InsertNext![1].Name.Should().Be(CommandNames.RunVerifyPhase);
        pipeline.TryGet<string>(ContextKeys.VerifyNotes, out var notes).Should().BeTrue();
        notes.Should().Contain("removed-but-still-referenced");
    }

    private static VerifyRoundHandler BuildSut(Queue<string> responses)
    {
        var stubChat = new StubChatClient(responses);
        var stubFactory = new StubChatClientFactory(stubChat);
        var tokenizer = new ActivationExpressionTokenizer();
        var parser = new ActivationExpressionParser(tokenizer);
        var evaluator = new ActivationEvaluator();
        var filter = new ActivationSkillFilter(parser, evaluator, NullLogger<ActivationSkillFilter>.Instance);
        var bodyResolver = new Mock<ISkillBodyResolver>();
        bodyResolver
            .Setup(b => b.ResolveBody(It.IsAny<RoleSkillDefinition>(), It.IsAny<SkillRole>()))
            .Returns<RoleSkillDefinition, SkillRole>((s, _) => $"Body for {s.Name}");
        return new VerifyRoundHandler(
            stubFactory, filter, bodyResolver.Object,
            RunStateConceptsTestFactory.Default,
            Mock.Of<AgentSmith.Contracts.Decisions.IDecisionLogger>(),
            new LoopLimitsConfig(),
            NullLogger<VerifyRoundHandler>.Instance);
    }

    private static PipelineContext PipelineFor(IReadOnlyList<RoleSkillDefinition> roles)
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.ResolvedPipeline, new ResolvedPipelineConfig(
            "fix-bug", new AgentConfig(), "skills/coding", null));
        pipeline.Set(ContextKeys.PlanJson,
            """{"summary":"fix it","scope":{"files":["a.cs"]},"steps":[],"status":"complete"}""");
        pipeline.Set(ContextKeys.DiffJson,
            """{"changes":[{"file":"a.cs","operation":"modify"}]}""");
        pipeline.Set(ContextKeys.AvailableRoles, roles);
        var concepts = RunStateConceptsTestFactory.Default(pipeline);
        concepts.SetEnum("pipeline_name", "fix-bug");
        return pipeline;
    }

    private static RoleSkillDefinition VerifierRole(string name) => new()
    {
        Name = name,
        DisplayName = name,
        Rules = "body",
        Role = "investigator",
        InvestigatorMode = "verify_diff",
        OutputSchema = "observation",
        ActivatesWhen = "pipeline_name = \"fix-bug\" OR pipeline_name = \"feature-implementation\"",
    };
}
