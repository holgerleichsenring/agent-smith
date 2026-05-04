using AgentSmith.Application.Services.Triage;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Skills;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Triage;

public sealed class StructuredTriageStrategyTests
{
    [Fact]
    public async Task ExecuteAsync_NoSkillsLoaded_FailsFastWithoutLlmCall()
    {
        var producerMock = new Mock<ITriageOutputProducer>(MockBehavior.Strict);
        var llmClientMock = new Mock<ILlmClient>(MockBehavior.Strict);
        var sut = new StructuredTriageStrategy(
            producerMock.Object,
            new PhaseCommandExpander(),
            NullLogger<StructuredTriageStrategy>.Instance);
        var pipeline = new PipelineContext();
        // No AvailableRoles in context — simulates skill-loader rejecting all skills.

        var result = await sut.ExecuteAsync(pipeline, llmClientMock.Object, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("No skills loaded");
        producerMock.VerifyNoOtherCalls(); // No LLM call attempted
    }

    [Fact]
    public async Task ExecuteAsync_EmptySkillList_FailsFastWithoutLlmCall()
    {
        var producerMock = new Mock<ITriageOutputProducer>(MockBehavior.Strict);
        var llmClientMock = new Mock<ILlmClient>(MockBehavior.Strict);
        var sut = new StructuredTriageStrategy(
            producerMock.Object,
            new PhaseCommandExpander(),
            NullLogger<StructuredTriageStrategy>.Instance);
        var pipeline = new PipelineContext();
        pipeline.Set<IReadOnlyList<RoleSkillDefinition>>(
            ContextKeys.AvailableRoles, Array.Empty<RoleSkillDefinition>());

        var result = await sut.ExecuteAsync(pipeline, llmClientMock.Object, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("No skills loaded");
        producerMock.VerifyNoOtherCalls();
    }
}
