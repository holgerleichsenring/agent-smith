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

/// <summary>2026-10-08-e8b9b: a keyword comment asks for a rework before the status gate;
/// 2026-10-08-0781: on a ticket with a code attempt it nudges the ticket and returns.</summary>
public sealed class KeywordCommentRouterTests
{
    private readonly Mock<ISpawnPipelineRunsUseCase> _spawn = new();
    private readonly Mock<IPreviousAttemptReader> _attempts = new();
    private readonly Mock<IReworkNudges> _nudges = new();

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
        var dispatcher = new WebhookSpawnDispatcher(_spawn.Object, factory.Object, NullLogger<WebhookSpawnDispatcher>.Instance);
        return new KeywordCommentRouter(dispatcher, _attempts.Object, _nudges.Object, NullLogger<KeywordCommentRouter>.Instance);
    }

    private void Attempted() => _attempts.Setup(a => a.LatestAsync("p", "T-1", null, It.IsAny<CancellationToken>()))
        .ReturnsAsync(new PreviousAttempt("run-1", "failed", DateTimeOffset.UtcNow.AddHours(-1), true));

    private Task<WebhookResult> Route(string status, string body = "@agent-smith please fix the name",
        Dictionary<string, string>? answers = null) =>
        Router().RouteAsync(Config(), [new ProjectMatch("p", "code", "jira")],
            new KeywordComment(new IncomingTicketEnvelope { TicketId = "T-1", Platform = "jira" }, status, body, answers,
                new ReworkAct("alice", DateTimeOffset.UtcNow)), CancellationToken.None);

    [Fact]
    public async Task Router_KeywordOnAttemptedTicket_NudgesNoHostCall()
    {
        Attempted();

        (await Route("Failed")).Handled.Should().BeTrue();

        _nudges.Verify(n => n.EnqueueAsync(It.Is<ReworkNudgeRequest>(r => r.Project == "p" && r.TicketId == "T-1"
            && r.Origin == ReworkNudgeOrigin.Ticket && r.Channel == ReworkChannel.Ticket), It.IsAny<CancellationToken>()), Times.Once);
        _spawn.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Router_NoAttempt_KeepsTheStatusGatedDispatch()
    {
        await Route("To Do");

        _spawn.Verify(s => s.ExecuteAsync(It.IsAny<AgentSmithConfig>(), It.IsAny<ResolvedProject>(), "code",
            It.IsAny<IncomingTicketEnvelope>(), It.IsAny<WebhookTriggerConfig>(), It.IsAny<CancellationToken>(),
            It.IsAny<Dictionary<string, string>?>()), Times.Once);
        _nudges.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Webhook_PlanAnswerComment_StaysInline()
    {
        Attempted();

        await Route("To Do", "Q1: yes", new Dictionary<string, string> { ["1"] = "yes" });

        _nudges.VerifyNoOtherCalls();
        _spawn.Verify(s => s.ExecuteAsync(It.IsAny<AgentSmithConfig>(), It.IsAny<ResolvedProject>(), "code",
            It.IsAny<IncomingTicketEnvelope>(), It.IsAny<WebhookTriggerConfig>(), It.IsAny<CancellationToken>(),
            It.IsAny<Dictionary<string, string>?>()), Times.Once);
    }

    [Fact]
    public async Task Router_NoKeyword_NotHandled()
    {
        (await Route("Done", "just a note")).Handled.Should().BeFalse();
        _nudges.VerifyNoOtherCalls();
    }

    [Fact]
    public void Refusal_Text_IsOursAndOmitsKeyword()
    {
        var text = ReworkTexts.TicketRefusal("run run-9 is working on this ticket");

        AgentSmith.Application.Services.Prompts.OwnTicketComment.IsOurs(text).Should().BeTrue();
        text.Should().NotContain("@agent-smith");
    }

    [Fact]
    public void LiveRunText_PromisesPickupNeverAsksAgain()
    {
        foreach (var text in new[] { ReworkTexts.TicketLiveRun("run-9"), ReworkTexts.PrLiveRun("12", "run-9") })
        {
            text.Should().Contain("picked up when it finishes");
            text.Should().NotContain("again");
        }
    }
}
