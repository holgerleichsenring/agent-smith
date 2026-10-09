using AgentSmith.Application.Services.Rework;
using AgentSmith.Application.Services.Specs;
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

/// <summary>2026-10-08-2123: the Jira status entry runs after the assignee handler — one delivery, one dispatch.</summary>
public sealed class JiraStatusWebhookTests
{
    private const string Payload = """
        { "webhookEvent": "jira:issue_updated", "timestamp": 1791460800000,
          "user": { "accountId": "alice-id", "displayName": "Alice" },
          "issue": { "key": "A-1", "fields": { "status": { "name": "To Do" }, "labels": [] } },
          "changelog": { "items": [ { "field": "assignee", "toString": "Agent Smith" }, { "field": "status", "toString": "To Do" } ] } }
        """;

    [Fact]
    public async Task JiraStatusWebhook_AssigneeAndStatusInOneEvent_OneDispatch()
    {
        var config = new AgentSmithConfig
        {
            Projects = new Dictionary<string, ResolvedProject>
            {
                ["p"] = new()
                {
                    Name = "p", Tracker = new TrackerConnection { Name = "jira", Type = TrackerType.Jira },
                    JiraTrigger = new JiraTriggerConfig { TriggerStatuses = ["To Do"], DoneStatus = "Done", AssigneeName = "Agent Smith" },
                },
            },
        };
        var loader = new Mock<IConfigurationLoader>();
        loader.Setup(l => l.LoadConfig(It.IsAny<string>())).Returns(config);
        var resolver = new Mock<IEnvelopeProjectResolver>();
        resolver.Setup(r => r.Resolve(It.IsAny<AgentSmithConfig>(), It.IsAny<IncomingTicketEnvelope>())).Returns([new ProjectMatch("p", "code", "jira")]);
        var spawn = new Mock<ISpawnPipelineRunsUseCase>();
        spawn.Setup(s => s.ExecuteAsync(It.IsAny<AgentSmithConfig>(), It.IsAny<ResolvedProject>(), It.IsAny<string>(),
                It.IsAny<IncomingTicketEnvelope>(), It.IsAny<WebhookTriggerConfig>(), It.IsAny<CancellationToken>(),
                It.IsAny<Dictionary<string, string>?>()))
            .ReturnsAsync(new SpawnResult(Array.Empty<ClaimResult>()));
        var tickets = new Mock<ITicketProviderFactory>();
        var dispatcher = new WebhookSpawnDispatcher(spawn.Object, tickets.Object, NullLogger<WebhookSpawnDispatcher>.Instance);
        var attempts = new Mock<IPreviousAttemptReader>();
        attempts.Setup(a => a.LatestAsync("p", "A-1", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PreviousAttempt("run-1", "success", DateTimeOffset.UnixEpoch, true));
        IWebhookHandler[] handlers =
        [
            new JiraAssigneeWebhookHandler(loader.Object, new ServerContext("c.yml"), resolver.Object, dispatcher,
                ApprovedRecordProbes.None(), NullLogger<JiraAssigneeWebhookHandler>.Instance),
            new JiraStatusWebhookHandler(loader.Object, new ServerContext("c.yml"), resolver.Object, ApprovedRecordProbes.None(),
                new StatusBackGate(attempts.Object, new TrackerIdentity(tickets.Object, NullLogger<TrackerIdentity>.Instance), dispatcher),
                NullLogger<JiraStatusWebhookHandler>.Instance),
        ];

        foreach (var handler in handlers)
            if ((await handler.HandleAsync(Payload, new Dictionary<string, string>())).Handled) break;

        spawn.Verify(s => s.ExecuteAsync(It.IsAny<AgentSmithConfig>(), It.IsAny<ResolvedProject>(), It.IsAny<string>(),
            It.IsAny<IncomingTicketEnvelope>(), It.IsAny<WebhookTriggerConfig>(), It.IsAny<CancellationToken>(),
            It.IsAny<Dictionary<string, string>?>()), Times.Once);
    }
}
