using AgentSmith.Application.Services.Dialogue;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Triggers;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Server.Contracts;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services;
using AgentSmith.Server.Services.Adapters;
using AgentSmith.Server.Services.ChatLaunch;
using AgentSmith.Server.Services.Handlers;
using AgentSmith.Infrastructure.Models;
using FluentAssertions;
using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Text.Json.Nodes;

namespace AgentSmith.Tests.Dispatcher;

public sealed class SlackModalSubmissionHandlerTests : IDisposable
{
    private readonly TestSupport.ChatRunHarness _chat = new();
    private readonly Mock<ISpawnPipelineRunsUseCase> _spawn = new();
    private readonly Mock<IPlatformAdapter> _adapter = new();
    private readonly Mock<IConfigurationLoader> _configLoader = new();
    private readonly Mock<ITicketProviderFactory> _ticketFactory = new();
    // 2026-09-15-9033: the handler holds the SLACK adapter by type now, so what it says
    // is read off the Slack API call it makes rather than off a mocked interface — which
    // is also the only way to see that the reply reaches Slack at all.
    private readonly RecordingSlackApi _slackApi = new();
    private readonly SlackModalSubmissionHandler _sut;

    public SlackModalSubmissionHandlerTests()
    {
        _adapter.Setup(a => a.Platform).Returns("slack");
        _configLoader
            .Setup(l => l.LoadConfig(It.IsAny<string>()))
            .Returns(new AgentSmithConfig
            {
                Projects = new()
                {
                    ["my-project"] = new ResolvedProject
                    {
                        Name = "my-project",
                        Repos = [new RepoConnection { Name = "repo-a" }],
                        Tracker = new TrackerConnection { Type = TrackerType.GitHub },
                    },
                },
            });
        _spawn.Setup(s => s.ExecuteAsync(
                It.IsAny<AgentSmithConfig>(), It.IsAny<ResolvedProject>(), It.IsAny<string>(),
                It.IsAny<IncomingTicketEnvelope>(), It.IsAny<WebhookTriggerConfig>(),
                It.IsAny<CancellationToken>(), It.IsAny<Dictionary<string, string>?>()))
            .ReturnsAsync(new SpawnResult([ClaimResult.Claimed()], "run-1"));
        var serverContext = new ServerContext("/tmp/agentsmith.yml");
        var fixHandler = new FixTicketIntentHandler(
            new ChatTicketRunLauncher(
                _configLoader.Object, serverContext, _spawn.Object,
                NullLogger<ChatTicketRunLauncher>.Instance),
            _chat.Get<ChatRunStart>());

        var listHandler = new ListTicketsIntentHandler(
            _adapter.Object,
            _configLoader.Object,
            _ticketFactory.Object,
            NullLogger<ListTicketsIntentHandler>.Instance);

        var createHandler = new CreateTicketIntentHandler(
            _adapter.Object,
            _configLoader.Object,
            _ticketFactory.Object,
            TestSupport.ApprovedSetDoubles.Kinds(),
            NullLogger<CreateTicketIntentHandler>.Instance);

        // The init and security-review doors write run rows; their own tests drive them over
        // a real store (ChatRunLaunchTests). Nothing below selects either command.
        _sut = new SlackModalSubmissionHandler(
            fixHandler,
            listHandler,
            createHandler,
            null!,
            null!,
            new SlackAdapter(
                new SlackApiClient(
                    new HttpClient(_slackApi),
                    new SlackAdapterOptions { BotToken = "test-token" },
                    NullLogger<SlackApiClient>.Instance),
                new SlackTypedQuestionBlockBuilder(),
                new SlackMessageBlockBuilder(),
                new SlackProgressFormatter(),
                NullLogger<SlackAdapter>.Instance),
            NullLogger<SlackModalSubmissionHandler>.Instance);
    }

    [Theory]
    [InlineData("fix_bug")]
    [InlineData("fix_bug_no_tests")]
    [InlineData("add_feature")]
    public async Task HandleAsync_CodingCommand_StartsTheCodePipelineThroughTheSpawnFunnel(string command)
    {
        var payload = BuildPayload(command, "my-project", ticketId: "42");

        await _sut.HandleAsync(payload, CancellationToken.None);

        _spawn.Verify(s => s.ExecuteAsync(
            It.IsAny<AgentSmithConfig>(),
            It.Is<ResolvedProject>(p => p.Name == "my-project"),
            "code",
            It.Is<IncomingTicketEnvelope>(e => e.TicketId == "42" && e.RequestedByName),
            It.IsAny<WebhookTriggerConfig>(),
            It.IsAny<CancellationToken>(), It.IsAny<Dictionary<string, string>?>()), Times.Once);
        _chat.Slack.Posts.Should().ContainSingle(p => p.Thread.ChannelId == "C123")
            .Which.Text.Should().Contain("run-1");
    }

    public void Dispose() => _chat.Dispose();

    [Fact]
    public async Task HandleAsync_MadDiscussion_StartsMadDiscussionOnTheTicket()
    {
        var payload = BuildPayload("mad_discussion", "my-project", ticketId: "58");

        await _sut.HandleAsync(payload, CancellationToken.None);

        _spawn.Verify(s => s.ExecuteAsync(
            It.IsAny<AgentSmithConfig>(), It.IsAny<ResolvedProject>(), "mad-discussion",
            It.Is<IncomingTicketEnvelope>(e => e.TicketId == "58"),
            It.IsAny<WebhookTriggerConfig>(),
            It.IsAny<CancellationToken>(), It.IsAny<Dictionary<string, string>?>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_LegalAnalysis_IsNoLongerACommand()
    {
        var payload = BuildPayload("legal_analysis", "my-project");

        await _sut.HandleAsync(payload, CancellationToken.None);

        _slackApi.Posts.Should().ContainSingle().Which.Should().Contain("Invalid command");
        _spawn.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task HandleAsync_MissingProject_SendsError()
    {
        var payload = BuildPayload("fix_bug", project: null, ticketId: "42");

        await _sut.HandleAsync(payload, CancellationToken.None);

        _slackApi.Posts.Should().ContainSingle()
            .Which.Should().Contain("C123").And.Contain("select a project");
    }

    [Fact]
    public async Task HandleAsync_InvalidCommand_SendsError()
    {
        var payload = BuildPayload("unknown_command", "my-project");

        await _sut.HandleAsync(payload, CancellationToken.None);

        _slackApi.Posts.Should().ContainSingle()
            .Which.Should().Contain("C123").And.Contain("Invalid command");
    }

    [Fact]
    public async Task HandleAsync_FixBug_MissingTicket_SendsError()
    {
        var payload = BuildPayload("fix_bug", "my-project");

        await _sut.HandleAsync(payload, CancellationToken.None);

        _slackApi.Posts.Should().ContainSingle()
            .Which.Should().Contain("C123").And.Contain("select a ticket");
    }

    [Fact]
    public async Task HandleAsync_MissingPrivateMetadata_DoesNotThrow()
    {
        var payload = new JsonObject
        {
            ["view"] = new JsonObject
            {
                ["private_metadata"] = "",
                ["state"] = new JsonObject { ["values"] = new JsonObject() }
            }
        };

        var act = async () => await _sut.HandleAsync(payload, CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    /// <summary>
    /// Captures what the Slack adapter posts, and answers ok so the adapter's own error
    /// path stays out of the way.
    /// </summary>
    private sealed class RecordingSlackApi : HttpMessageHandler
    {
        public List<string> Posts { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Posts.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"ok\":true}"),
            };
        }
    }

    private static JsonNode BuildPayload(
        string command, string? project,
        string? ticketId = null, string? title = null,
        string? description = null)
    {
        var values = new JsonObject();

        values[DispatcherDefaults.SlackBlockCommand] = new JsonObject
        {
            [DispatcherDefaults.SlackActionCommand] = new JsonObject
            {
                ["selected_option"] = new JsonObject
                {
                    ["value"] = command
                }
            }
        };

        if (project is not null)
        {
            values[DispatcherDefaults.SlackBlockProject] = new JsonObject
            {
                [DispatcherDefaults.SlackActionProject] = new JsonObject
                {
                    ["selected_option"] = new JsonObject
                    {
                        ["value"] = project
                    }
                }
            };
        }
        else
        {
            values[DispatcherDefaults.SlackBlockProject] = new JsonObject
            {
                [DispatcherDefaults.SlackActionProject] = new JsonObject
                {
                    ["selected_option"] = (JsonNode?)null
                }
            };
        }

        if (ticketId is not null)
        {
            values[DispatcherDefaults.SlackBlockTicket] = new JsonObject
            {
                [DispatcherDefaults.SlackActionTicket] = new JsonObject
                {
                    ["selected_option"] = new JsonObject
                    {
                        ["value"] = ticketId
                    }
                }
            };
        }

        if (title is not null)
        {
            values[DispatcherDefaults.SlackBlockTitle] = new JsonObject
            {
                ["title_input"] = new JsonObject { ["value"] = title }
            };
        }

        if (description is not null)
        {
            values[DispatcherDefaults.SlackBlockDescription] = new JsonObject
            {
                ["desc_input"] = new JsonObject { ["value"] = description }
            };
        }

        return new JsonObject
        {
            ["view"] = new JsonObject
            {
                ["private_metadata"] = "{\"channel_id\":\"C123\",\"user_id\":\"U456\"}",
                ["state"] = new JsonObject
                {
                    ["values"] = values
                }
            }
        };
    }
}
