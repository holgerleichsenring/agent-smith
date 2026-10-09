using AgentSmith.Application.Services.Rework;
using AgentSmith.Application.Services.Triage;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Triggers;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Server.Services.Webhooks;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Rework.StatusBack;

/// <summary>2026-10-08-2123: a status-change delivery claims only a person's move back of a finished ticket.</summary>
public sealed class StatusBackGateTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 8, 10, 0, 0, TimeSpan.Zero);
    private readonly Mock<IPreviousAttemptReader> _attempts = new();
    private readonly Mock<ISpawnPipelineRunsUseCase> _spawn = new();
    private readonly Mock<ITicketProvider> _ticket = new();

    public StatusBackGateTests()
    {
        _spawn.Setup(s => s.ExecuteAsync(It.IsAny<AgentSmithConfig>(), It.IsAny<ResolvedProject>(), It.IsAny<string>(),
                It.IsAny<IncomingTicketEnvelope>(), It.IsAny<WebhookTriggerConfig>(), It.IsAny<CancellationToken>(),
                It.IsAny<Dictionary<string, string>?>()))
            .ReturnsAsync(new SpawnResult(Array.Empty<ClaimResult>()));
        _ticket.As<ITrackerSelf>().Setup(t => t.SelfAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new TrackerActor("bot-id", "agent"));
    }

    private static AgentSmithConfig Config(params string[] statuses) => new()
    {
        Projects = new Dictionary<string, ResolvedProject>
        {
            ["p"] = new()
            {
                Name = "p", Tracker = new TrackerConnection { Name = "jira", Type = TrackerType.Jira },
                JiraTrigger = new JiraTriggerConfig { TriggerStatuses = [.. statuses], DoneStatus = "Done" },
            },
        },
    };

    private StatusBackGate Gate()
    {
        var tickets = new Mock<ITicketProviderFactory>();
        tickets.Setup(t => t.Create(It.IsAny<TrackerConnection>())).Returns(_ticket.Object);
        var dispatcher = new WebhookSpawnDispatcher(_spawn.Object, tickets.Object, NullLogger<WebhookSpawnDispatcher>.Instance);
        return new StatusBackGate(_attempts.Object, new TrackerIdentity(tickets.Object, NullLogger<TrackerIdentity>.Instance), dispatcher);
    }

    private void Finished() => _attempts.Setup(a => a.LatestAsync("p", "T-1", null, It.IsAny<CancellationToken>()))
        .ReturnsAsync(new PreviousAttempt("run-1", "success", Start, true) { ActsReadAt = Start.AddMinutes(1) });

    private Task<WebhookResult> Move(AgentSmithConfig config, DateTimeOffset at, string actor = "alice") =>
        Gate().DispatchAsync(config, [new ProjectMatch("p", "code", "jira")], new IncomingTicketEnvelope { TicketId = "T-1", Platform = "jira" },
            "To Do", at, new TrackerActor(actor, actor), CancellationToken.None);

    private void VerifySpawned(Times times) => _spawn.Verify(s => s.ExecuteAsync(It.IsAny<AgentSmithConfig>(), It.IsAny<ResolvedProject>(),
        It.IsAny<string>(), It.IsAny<IncomingTicketEnvelope>(), It.IsAny<WebhookTriggerConfig>(), It.IsAny<CancellationToken>(),
        It.IsAny<Dictionary<string, string>?>()), times);

    [Fact]
    public async Task StatusBackGate_FinishedAndTriggering_Dispatches()
    {
        Finished();
        (await Move(Config("To Do"), Start.AddHours(2))).Handled.Should().BeTrue();
        VerifySpawned(Times.Once());
    }

    [Fact]
    public async Task StatusBackGate_FirstRunTicket_NotHandledNoComment()
    {
        (await Move(Config("To Do"), Start.AddHours(2))).Handled.Should().BeFalse();
        VerifySpawned(Times.Never());
        _ticket.Verify(t => t.UpdateStatusAsync(It.IsAny<AgentSmith.Domain.Models.TicketId>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task StatusBackGate_EmptyTriggerStatuses_NotHandled()
    {
        Finished();
        (await Move(Config(), Start.AddHours(2))).Handled.Should().BeFalse();
    }

    [Fact]
    public async Task StatusBackGate_OwnReopen_NotHandled()
    {
        Finished();
        (await Move(Config("To Do"), Start.AddHours(2), actor: "agent")).Handled.Should().BeFalse("Retry and the rework entry move as the token");
    }

    [Fact]
    public async Task StatusBackGate_RedeliveryAfterAttempt_NotHandled()
    {
        Finished();
        (await Move(Config("To Do"), Start.AddSeconds(30))).Handled.Should().BeFalse("a move the attempt already read is a redelivery");
    }
}
