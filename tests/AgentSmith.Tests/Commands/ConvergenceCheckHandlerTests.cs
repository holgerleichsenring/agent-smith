using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Commands;

public sealed class ConvergenceCheckHandlerTests
{
    private readonly Mock<ILlmClientFactory> _llmFactoryMock = new();
    private readonly Mock<ILlmClient> _llmClientMock = new();
    private readonly ConvergenceCheckHandler _handler;

    public ConvergenceCheckHandlerTests()
    {
        _llmFactoryMock.Setup(f => f.Create(It.IsAny<AgentConfig>()))
            .Returns(_llmClientMock.Object);
        var planConsolidator = new PlanConsolidator(
            _llmFactoryMock.Object,
            NullLogger<PlanConsolidator>.Instance);
        _handler = new ConvergenceCheckHandler(
            planConsolidator,
            _llmFactoryMock.Object,
            NullLogger<ConvergenceCheckHandler>.Instance);
    }

    [Fact]
    public async Task ExecuteAsync_NoDiscussionLog_ReturnsOk()
    {
        var pipeline = CreatePipeline();
        var context = CreateContext(pipeline);

        var result = await _handler.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("No discussion log");
    }

    [Fact]
    public async Task ExecuteAsync_AlreadyConverged_ReturnsNoOp()
    {
        var pipeline = CreatePipeline();
        pipeline.Set(ContextKeys.ConsolidatedPlan, "Already done");
        pipeline.Set(ContextKeys.DiscussionLog, new List<DiscussionEntry>
        {
            new("architect", "Architect", "🏗️", 1, "Plan AGREE")
        });
        var context = CreateContext(pipeline);

        var result = await _handler.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("Already converged");
    }

    [Fact]
    public async Task ExecuteAsync_AllAgree_ConsolidatesPlan()
    {
        var pipeline = CreatePipeline();
        pipeline.Set(ContextKeys.DiscussionLog, new List<DiscussionEntry>
        {
            new("architect", "Architect", "🏗️", 1, "My plan. AGREE"),
            new("tester", "Tester", "🧪", 1, "Looks good. AGREE")
        });

        SetupLlmResponse("1. Implement the feature\n2. Add tests");

        var context = CreateContext(pipeline);
        var result = await _handler.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("Consensus reached");
        pipeline.Has(ContextKeys.ConsolidatedPlan).Should().BeTrue();
        pipeline.Has(ContextKeys.Plan).Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_UnresolvedObjection_UnderMaxRounds_InsertsMoreRounds()
    {
        var pipeline = CreatePipeline();
        pipeline.Set(ContextKeys.DiscussionLog, new List<DiscussionEntry>
        {
            new("architect", "Architect", "🏗️", 1, "My plan"),
            new("tester", "Tester", "🧪", 1, "OBJECTION - needs tests")
        });

        var context = CreateContext(pipeline);
        var result = await _handler.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.InsertNext.Should().NotBeNull();
        result.InsertNext!.Select(c => c.DisplayName).Should().Contain("SkillRoundCommand:tester:2");
        result.InsertNext.Select(c => c.DisplayName).Should().Contain("ConvergenceCheckCommand");
    }

    [Fact]
    public async Task ExecuteAsync_UnresolvedObjection_AtMaxRounds_Escalates()
    {
        var pipeline = CreatePipeline();
        pipeline.Set(ContextKeys.ProjectSkills, new SkillConfig
        {
            Discussion = new DiscussionConfig { MaxRounds = 2 }
        });
        pipeline.Set(ContextKeys.DiscussionLog, new List<DiscussionEntry>
        {
            new("architect", "Architect", "🏗️", 1, "Plan v1"),
            new("tester", "Tester", "🧪", 1, "OBJECTION"),
            new("architect", "Architect", "🏗️", 2, "Plan v2"),
            new("tester", "Tester", "🧪", 2, "Still OBJECTION")
        });

        SetupLlmResponse("Consolidated with dissent");

        var context = CreateContext(pipeline);
        var result = await _handler.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("No consensus");
        result.Message.Should().Contain("Escalating");
        result.InsertNext.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_MixedAgreeAndSuggestion_Converges()
    {
        var pipeline = CreatePipeline();
        pipeline.Set(ContextKeys.DiscussionLog, new List<DiscussionEntry>
        {
            new("architect", "Architect", "🏗️", 1, "My plan. AGREE"),
            new("tester", "Tester", "🧪", 1, "Minor tweak. SUGGESTION")
        });

        SetupLlmResponse("1. Do this\n2. Do that");

        var context = CreateContext(pipeline);
        var result = await _handler.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("Consensus");
        result.InsertNext.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_ConsolidationSetssPlanSteps()
    {
        var pipeline = CreatePipeline();
        pipeline.Set(ContextKeys.DiscussionLog, new List<DiscussionEntry>
        {
            new("architect", "Architect", "🏗️", 1, "AGREE")
        });

        SetupLlmResponse("- Add endpoint\n- Write tests\n- Update docs");

        var context = CreateContext(pipeline);
        await _handler.ExecuteAsync(context, CancellationToken.None);

        var plan = pipeline.Get<Plan>(ContextKeys.Plan);
        plan.Steps.Should().HaveCount(3);
        plan.Steps[0].Order.Should().Be(1);
        plan.Steps[0].Description.Should().Be("Add endpoint");
    }

    [Fact]
    public async Task ExecuteAsync_ConsolidationFails_PlanNotStored()
    {
        var pipeline = CreatePipeline();
        pipeline.Set(ContextKeys.DiscussionLog, new List<DiscussionEntry>
        {
            new("architect", "Architect", "🏗️", 1, "AGREE")
        });

        _llmClientMock.Setup(c => c.CompleteAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<TaskType>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("LLM error"));

        var context = CreateContext(pipeline);
        var result = await _handler.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        pipeline.Has(ContextKeys.ConsolidatedPlan).Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_SkillObservations_AllNonBlocking_ConsensusIsTrue()
    {
        var pipeline = CreatePipeline();
        var observations = new List<SkillObservation>
        {
            new(1, "architect", ObservationConcern.Architecture, "Good structure", "Keep it", false, ObservationSeverity.Info, 80),
            new(2, "tester", ObservationConcern.Correctness, "Tests pass", "Add more", false, ObservationSeverity.Low, 75)
        };
        pipeline.Set(ContextKeys.SkillObservations, observations);
        pipeline.Set(ContextKeys.DiscussionLog, new List<DiscussionEntry>
        {
            new("architect", "Architect", "🏗️", 1, "observations text")
        });

        SetupLlmResponse("""{ "consensus": true, "links": [], "additionalRoles": [] }""");

        var context = CreateContext(pipeline);
        var result = await _handler.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("Consensus reached");
        pipeline.Has(ContextKeys.ConvergenceResult).Should().BeTrue();
        var convergence = pipeline.Get<ConvergenceResult>(ContextKeys.ConvergenceResult);
        convergence.Consensus.Should().BeTrue();
        convergence.Blocking.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_SkillObservations_ContradictingBlocking_ConsensusIsFalse()
    {
        var pipeline = CreatePipeline();
        pipeline.Set(ContextKeys.ProjectSkills, new SkillConfig
        {
            Discussion = new DiscussionConfig { MaxRounds = 1 }
        });
        var observations = new List<SkillObservation>
        {
            new(1, "architect", ObservationConcern.Security, "Use OAuth", "Implement OAuth", true, ObservationSeverity.High, 90),
            new(2, "tester", ObservationConcern.Security, "Use API keys", "Implement API keys", true, ObservationSeverity.High, 85)
        };
        pipeline.Set(ContextKeys.SkillObservations, observations);
        pipeline.Set(ContextKeys.DiscussionLog, new List<DiscussionEntry>
        {
            new("architect", "Architect", "🏗️", 1, "text")
        });

        SetupLlmResponse("""
            {
              "consensus": false,
              "links": [
                { "observationId": 1, "relatedObservationId": 2, "relationship": "contradicts" }
              ],
              "additionalRoles": []
            }
            """);

        var context = CreateContext(pipeline);
        var result = await _handler.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("No consensus");
        pipeline.Has(ContextKeys.ConvergenceResult).Should().BeTrue();
        var convergence = pipeline.Get<ConvergenceResult>(ContextKeys.ConvergenceResult);
        convergence.Consensus.Should().BeFalse();
        convergence.Links.Should().ContainSingle(l => l.Relationship == ObservationRelationship.Contradicts);
    }

    [Fact]
    public async Task ExecuteAsync_SkillObservations_BlockingBelowConfidence70_ConsensusIsFalse()
    {
        var pipeline = CreatePipeline();
        pipeline.Set(ContextKeys.ProjectSkills, new SkillConfig
        {
            Discussion = new DiscussionConfig { MaxRounds = 1 }
        });
        var observations = new List<SkillObservation>
        {
            new(1, "architect", ObservationConcern.Architecture, "Might be wrong", "Maybe fix", true, ObservationSeverity.Medium, 50)
        };
        pipeline.Set(ContextKeys.SkillObservations, observations);
        pipeline.Set(ContextKeys.DiscussionLog, new List<DiscussionEntry>
        {
            new("architect", "Architect", "🏗️", 1, "text")
        });

        // LLM says consensus, but low confidence on blocking should override
        SetupLlmResponse("""{ "consensus": true, "links": [], "additionalRoles": [] }""");

        var context = CreateContext(pipeline);
        var result = await _handler.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var convergence = pipeline.Get<ConvergenceResult>(ContextKeys.ConvergenceResult);
        convergence.Consensus.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_SkillObservations_MissingRoleForConcern_AddsToAdditionalRoles()
    {
        var pipeline = CreatePipeline();
        pipeline.Set(ContextKeys.ProjectSkills, new SkillConfig
        {
            Discussion = new DiscussionConfig { MaxRounds = 1 }
        });
        var observations = new List<SkillObservation>
        {
            new(1, "architect", ObservationConcern.Legal, "Licensing issue", "Check license", false, ObservationSeverity.Medium, 70)
        };
        pipeline.Set(ContextKeys.SkillObservations, observations);
        pipeline.Set(ContextKeys.DiscussionLog, new List<DiscussionEntry>
        {
            new("architect", "Architect", "🏗️", 1, "text")
        });

        SetupLlmResponse("""{ "consensus": true, "links": [], "additionalRoles": ["legal-reviewer"] }""");

        var context = CreateContext(pipeline);
        var result = await _handler.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var convergence = pipeline.Get<ConvergenceResult>(ContextKeys.ConvergenceResult);
        convergence.AdditionalRoles.Should().Contain("legal-reviewer");
    }

    [Fact]
    public async Task ExecuteAsync_SkillObservations_ProducesConvergenceResult_NotConsolidatedPlanString()
    {
        var pipeline = CreatePipeline();
        var observations = new List<SkillObservation>
        {
            new(1, "architect", ObservationConcern.Architecture, "Good", "Keep", false, ObservationSeverity.Info, 80)
        };
        pipeline.Set(ContextKeys.SkillObservations, observations);
        pipeline.Set(ContextKeys.DiscussionLog, new List<DiscussionEntry>
        {
            new("architect", "Architect", "🏗️", 1, "text")
        });

        SetupLlmResponse("""{ "consensus": true, "links": [], "additionalRoles": [] }""");

        var context = CreateContext(pipeline);
        await _handler.ExecuteAsync(context, CancellationToken.None);

        pipeline.Has(ContextKeys.ConvergenceResult).Should().BeTrue();
        var convergence = pipeline.Get<ConvergenceResult>(ContextKeys.ConvergenceResult);
        convergence.Should().NotBeNull();
        convergence.Observations.Should().HaveCount(1);
    }

    private void SetupLlmResponse(string response)
    {
        _llmClientMock.Setup(c => c.CompleteAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<TaskType>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse(response, 0, 0));
    }

    private static ConvergenceCheckContext CreateContext(PipelineContext pipeline)
    {
        return new ConvergenceCheckContext(
            new AgentConfig { Type = "claude" },
            pipeline);
    }

    private static PipelineContext CreatePipeline()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.Ticket, new Ticket(
            new TicketId("42"), "Fix bug", "Description", null, "Open", "github"));
        return pipeline;
    }
}
