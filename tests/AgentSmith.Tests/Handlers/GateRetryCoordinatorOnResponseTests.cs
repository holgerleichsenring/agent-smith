using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Skills;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Handlers;

/// <summary>
/// p0142: closes the deferred-3x debt from p0132a/b/c — every gate retry
/// attempt invokes onResponse exactly once so per-attempt cost attribution
/// is now load-bearing.
/// </summary>
public sealed class GateRetryCoordinatorOnResponseTests
{
    [Fact]
    public async Task ExecuteAsync_NoOnResponse_RunsExistingPath()
    {
        var (sut, _) = Build(passesGate: true);

        var outcome = await sut.ExecuteAsync(
            MakeRole(), MakeOrch(), "sys", "prefix", "suffix",
            new PipelineContext().WithAgent(),
            CancellationToken.None);

        outcome.Result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_OnResponseProvided_InvokedOncePerAttempt()
    {
        var (sut, _) = Build(passesGate: true);
        var responses = new List<ChatResponse>();

        await sut.ExecuteAsync(
            MakeRole(), MakeOrch(), "sys", "prefix", "suffix",
            new PipelineContext().WithAgent(),
            CancellationToken.None,
            onResponse: r => responses.Add(r));

        responses.Should().HaveCount(1, "first attempt passed the gate — no retry");
    }

    [Fact]
    public async Task ExecuteAsync_OnResponseProvided_InvokedOnRetryToo()
    {
        var (sut, _) = Build(passesGate: false);
        var responses = new List<ChatResponse>();

        await sut.ExecuteAsync(
            MakeRole(), MakeOrch(), "sys", "prefix", "suffix",
            new PipelineContext().WithAgent(),
            CancellationToken.None,
            onResponse: r => responses.Add(r));

        responses.Should().HaveCount(2, "first attempt failed gate — retry fires once more");
    }

    [Fact]
    public async Task ExecuteAsync_OnResponseThrows_GateLogicContinues()
    {
        var (sut, _) = Build(passesGate: true);

        var outcome = await sut.ExecuteAsync(
            MakeRole(), MakeOrch(), "sys", "prefix", "suffix",
            new PipelineContext().WithAgent(),
            CancellationToken.None,
            onResponse: _ => throw new InvalidOperationException("hook boom"));

        outcome.Result.IsSuccess.Should().BeTrue();
    }

    private static (GateRetryCoordinator Coord, StubChatClientFactory Factory) Build(bool passesGate)
    {
        var chat = new StubChatClient(new Queue<string>(["resp1", "resp2"]));
        var factory = new StubChatClientFactory(chat);
        var gateHandler = new Mock<IGateOutputHandler>();
        gateHandler.Setup(g => g.Handle(
                It.IsAny<RoleSkillDefinition>(), It.IsAny<SkillOrchestration>(),
                It.IsAny<string>(), It.IsAny<PipelineContext>()))
            .Returns(passesGate ? CommandResult.Ok("ok") : CommandResult.Fail("parse failed"));
        return (new GateRetryCoordinator(gateHandler.Object, factory,
            NullLogger<GateRetryCoordinator>.Instance), factory);
    }

    private static RoleSkillDefinition MakeRole() => new()
    {
        Name = "test-gate",
        DisplayName = "Test Gate",
        Emoji = "🚪",
        Role = "judge"
    };

    private static SkillOrchestration MakeOrch() => new(
        OrchestrationRole.Gate,
        SkillOutputType.Artifact,
        Array.Empty<string>(), Array.Empty<string>(),
        Array.Empty<string>(), Array.Empty<string>());
}

internal static class PipelineContextTestExtensions
{
    public static PipelineContext WithAgent(this PipelineContext p)
    {
        p.Set(ContextKeys.AgentConfig, new AgentConfig { Type = "test" });
        return p;
    }
}
