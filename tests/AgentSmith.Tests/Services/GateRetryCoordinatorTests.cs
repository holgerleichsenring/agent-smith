using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Services;

public sealed class GateRetryCoordinatorTests
{
    private readonly Mock<ILlmClient> _llm = new();
    private readonly GateOutputHandler _gate = new(NullLogger<GateOutputHandler>.Instance);
    private readonly GateRetryCoordinator _coordinator;

    public GateRetryCoordinatorTests()
    {
        _coordinator = new GateRetryCoordinator(_gate, NullLogger<GateRetryCoordinator>.Instance);
    }

    private static RoleSkillDefinition CreateRole() => new()
    {
        Name = "test-gate",
        DisplayName = "Test Gate",
        Emoji = "🔒"
    };

    private static SkillOrchestration CreateOrchestration() => new(
        SkillRole.Gate,
        SkillOutputType.List,
        Array.Empty<string>(),
        Array.Empty<string>(),
        Array.Empty<string>(),
        new[] { "*" });

    private void SetupLlmResponses(params string[] responses)
    {
        var calls = 0;
        _llm.Setup(c => c.CompleteAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<TaskType>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                var text = responses[Math.Min(calls, responses.Length - 1)];
                calls++;
                return new LlmResponse(text, 0, 0);
            });
    }

    [Fact]
    public async Task FirstAttemptSucceeds_NoRetry()
    {
        SetupLlmResponses("""{"confirmed":[],"rejected":[]}""");

        var outcome = await _coordinator.ExecuteAsync(
            CreateRole(), CreateOrchestration(), "sys", "user",
            _llm.Object, new PipelineContext(), CancellationToken.None);

        outcome.Result.IsSuccess.Should().BeTrue();
        _llm.Verify(c => c.CompleteAsync(
            It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<TaskType>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task FirstAttemptFails_RetriesWithCorrectivePrompt()
    {
        var capturedPrompts = new List<string>();
        _llm.Setup(c => c.CompleteAsync(
                It.IsAny<string>(), Capture.In(capturedPrompts),
                It.IsAny<TaskType>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string sys, string user, TaskType t, CancellationToken ct) =>
                capturedPrompts.Count == 1
                    ? new LlmResponse("garbage response", 0, 0)
                    : new LlmResponse("""{"confirmed":[],"rejected":[]}""", 0, 0));

        var outcome = await _coordinator.ExecuteAsync(
            CreateRole(), CreateOrchestration(), "sys", "user",
            _llm.Object, new PipelineContext(), CancellationToken.None);

        outcome.Result.IsSuccess.Should().BeTrue();
        capturedPrompts.Should().HaveCount(2);
        capturedPrompts[0].Should().Be("user");
        capturedPrompts[1].Should().Contain("previous response could not be parsed");
        capturedPrompts[1].Should().Contain("garbage response");
    }

    [Fact]
    public async Task RetryAlsoFails_ReturnsFail()
    {
        SetupLlmResponses("garbage", "still garbage");

        var outcome = await _coordinator.ExecuteAsync(
            CreateRole(), CreateOrchestration(), "sys", "user",
            _llm.Object, new PipelineContext(), CancellationToken.None);

        outcome.Result.IsSuccess.Should().BeFalse();
        outcome.Result.Message.Should().Contain("failed after one retry");
        _llm.Verify(c => c.CompleteAsync(
            It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<TaskType>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task RetryResponseIsReturnedInOutcome()
    {
        SetupLlmResponses("garbage", """{"confirmed":[],"rejected":[]}""");

        var outcome = await _coordinator.ExecuteAsync(
            CreateRole(), CreateOrchestration(), "sys", "user",
            _llm.Object, new PipelineContext(), CancellationToken.None);

        outcome.FinalResponseText.Should().Be("""{"confirmed":[],"rejected":[]}""");
    }
}
