using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Triggers;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using AgentSmith.Server.Contracts;
using AgentSmith.Server.Services.Rework;
using AgentSmith.Server.Services.Webhooks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Rework;

/// <summary>2026-10-08-e8b9b: a keyword comment asks the rework entry before the status gate.</summary>
public sealed class KeywordCommentRouterTests
{
    private readonly Mock<ISpawnPipelineRunsUseCase> _spawn = new();
    private readonly Mock<IReworkEntry> _rework = new();
    private readonly Mock<ITicketProvider> _ticket = new();

    private static AgentSmithConfig Config() => new()
    {
        Projects = new Dictionary<string, ResolvedProject>
        {
            ["p"] = new()
            {
                Name = "p", Tracker = new TrackerConnection { Type = TrackerType.Jira }, Repos = [new RepoConnection { Name = "p" }],
                JiraTrigger = new JiraTriggerConfig { CommentKeyword = "@agent-smith", TriggerStatuses = ["To Do"], DoneStatus = "Done", FailedStatus = "Failed" },
            },
        },
    };

    private KeywordCommentRouter Router()
    {
        _spawn.Setup(s => s.ExecuteAsync(It.IsAny<AgentSmithConfig>(), It.IsAny<ResolvedProject>(), It.IsAny<string>(),
                It.IsAny<IncomingTicketEnvelope>(), It.IsAny<WebhookTriggerConfig>(), It.IsAny<CancellationToken>(),
                It.IsAny<Dictionary<string, string>?>()))
            .ReturnsAsync(new SpawnResult(Array.Empty<ClaimResult>()));
        var factory = new Mock<ITicketProviderFactory>();
        factory.Setup(f => f.Create(It.IsAny<TrackerConnection>())).Returns(_ticket.Object);
        var dispatcher = new WebhookSpawnDispatcher(_spawn.Object, factory.Object, NullLogger<WebhookSpawnDispatcher>.Instance);
        return new KeywordCommentRouter(dispatcher, _rework.Object, factory.Object, NullLogger<KeywordCommentRouter>.Instance);
    }

    private void Outcome(ReworkOutcome outcome) => _rework.Setup(r => r.EnterAsync(It.IsAny<ResolvedProject>(), "T-1",
        It.IsAny<ReworkAct>(), "code", It.IsAny<CancellationToken>())).ReturnsAsync(outcome);

    private Task<WebhookResult> Route(string status, string body = "@agent-smith please fix the name") =>
        Router().RouteAsync(Config(), [new ProjectMatch("p", "code", "jira")],
            new KeywordComment(new IncomingTicketEnvelope { TicketId = "T-1", Platform = "jira" }, status, body, null,
                new ReworkAct("alice", DateTimeOffset.UtcNow)), CancellationToken.None);

    [Fact]
    public async Task Router_KeywordOnFailedStatus_StartsRework()
    {
        Outcome(ReworkOutcome.Started("run-2"));

        (await Route("Failed")).Handled.Should().BeTrue();

        _rework.Verify(r => r.EnterAsync(It.IsAny<ResolvedProject>(), "T-1", It.IsAny<ReworkAct>(), "code", It.IsAny<CancellationToken>()), Times.Once);
        _spawn.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Router_NotARework_KeepsTheStatusGatedDispatch()
    {
        Outcome(ReworkOutcome.NotARework);

        await Route("To Do");

        _spawn.Verify(s => s.ExecuteAsync(It.IsAny<AgentSmithConfig>(), It.IsAny<ResolvedProject>(), "code",
            It.IsAny<IncomingTicketEnvelope>(), It.IsAny<WebhookTriggerConfig>(), It.IsAny<CancellationToken>(),
            It.IsAny<Dictionary<string, string>?>()), Times.Once);
    }

    [Fact]
    public async Task Router_Refused_SaysSoOnTheTicket()
    {
        Outcome(ReworkOutcome.Refused("run run-9 is working on this ticket", "run-9"));

        await Route("Done");

        _ticket.Verify(t => t.UpdateStatusAsync(It.Is<TicketId>(id => id.Value == "T-1"),
            It.Is<string>(s => s.Contains("run-9")), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Router_NoKeyword_NotHandled()
    {
        (await Route("Done", "just a note")).Handled.Should().BeFalse();
        _rework.VerifyNoOtherCalls();
    }

    [Fact]
    public void Refusal_Text_IsOursAndOmitsKeyword()
    {
        var text = ReworkTexts.TicketRefusal("run run-9 is working on this ticket");

        AgentSmith.Application.Services.Prompts.OwnTicketComment.IsOurs(text).Should().BeTrue();
        text.Should().NotContain("@agent-smith");
    }
}
