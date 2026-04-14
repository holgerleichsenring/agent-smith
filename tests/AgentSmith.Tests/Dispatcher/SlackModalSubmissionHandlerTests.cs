using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Server.Contracts;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services;
using AgentSmith.Server.Services.Adapters;
using AgentSmith.Server.Services.Handlers;
using AgentSmith.Infrastructure.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StackExchange.Redis;
using System.Text.Json.Nodes;

namespace AgentSmith.Tests.Dispatcher;

public sealed class SlackModalSubmissionHandlerTests
{
    private readonly Mock<IJobSpawner> _spawner = new();
    private readonly Mock<IPlatformAdapter> _adapter = new();
    private readonly Mock<IConfigurationLoader> _configLoader = new();
    private readonly Mock<ITicketProviderFactory> _ticketFactory = new();
    private readonly SlackModalSubmissionHandler _sut;

    public SlackModalSubmissionHandlerTests()
    {
        _adapter.Setup(a => a.Platform).Returns("slack");

        // Build real handlers with mocked dependencies
        var redis = new Mock<IConnectionMultiplexer>();
        var db = new Mock<IDatabase>();
        redis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(db.Object);

        var stateManager = new ConversationStateManager(
            redis.Object,
            NullLogger<ConversationStateManager>.Instance);

        var messageBus = new Mock<IMessageBus>();
        var messageRouter = new Mock<IBusMessageRouter>();
        var listener = new MessageBusListener(
            messageBus.Object,
            messageRouter.Object,
            NullLogger<MessageBusListener>.Instance);

        var fixHandler = new FixTicketIntentHandler(
            _spawner.Object,
            _adapter.Object,
            stateManager,
            listener,
            NullLogger<FixTicketIntentHandler>.Instance);

        var listHandler = new ListTicketsIntentHandler(
            _adapter.Object,
            _configLoader.Object,
            _ticketFactory.Object,
            NullLogger<ListTicketsIntentHandler>.Instance);

        var createHandler = new CreateTicketIntentHandler(
            _adapter.Object,
            _configLoader.Object,
            _ticketFactory.Object,
            NullLogger<CreateTicketIntentHandler>.Instance);

        var initHandler = new InitProjectIntentHandler(
            _spawner.Object,
            _adapter.Object,
            stateManager,
            listener,
            NullLogger<InitProjectIntentHandler>.Instance);

        _sut = new SlackModalSubmissionHandler(
            fixHandler,
            listHandler,
            createHandler,
            initHandler,
            _adapter.Object,
            NullLogger<SlackModalSubmissionHandler>.Instance);
    }

    [Fact]
    public async Task HandleAsync_FixBug_SpawnsJobWithFixBugPipeline()
    {
        _spawner.Setup(s => s.SpawnAsync(
                It.IsAny<JobRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("job-123");

        var payload = BuildPayload("fix_bug", "my-project", ticketId: "42");

        await _sut.HandleAsync(payload, CancellationToken.None);

        _spawner.Verify(s => s.SpawnAsync(
            It.Is<JobRequest>(r =>
                r.InputCommand.Contains("#42") &&
                r.Project == "my-project" &&
                r.PipelineOverride == "fix-bug"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_FixBugNoTests_SpawnsJobWithFixNoTestPipeline()
    {
        _spawner.Setup(s => s.SpawnAsync(
                It.IsAny<JobRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("job-123");

        var payload = BuildPayload("fix_bug_no_tests", "my-project", ticketId: "42");

        await _sut.HandleAsync(payload, CancellationToken.None);

        _spawner.Verify(s => s.SpawnAsync(
            It.Is<JobRequest>(r => r.PipelineOverride == "fix-no-test"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_AddFeature_SpawnsJobWithAddFeaturePipeline()
    {
        _spawner.Setup(s => s.SpawnAsync(
                It.IsAny<JobRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("job-123");

        var payload = BuildPayload("add_feature", "my-project", ticketId: "58");

        await _sut.HandleAsync(payload, CancellationToken.None);

        _spawner.Verify(s => s.SpawnAsync(
            It.Is<JobRequest>(r =>
                r.InputCommand.Contains("#58") &&
                r.PipelineOverride == "add-feature"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_MissingProject_SendsError()
    {
        var payload = BuildPayload("fix_bug", project: null, ticketId: "42");

        await _sut.HandleAsync(payload, CancellationToken.None);

        _adapter.Verify(a => a.SendMessageAsync(
            "C123",
            It.Is<string>(s => s.Contains("select a project")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_InvalidCommand_SendsError()
    {
        var payload = BuildPayload("unknown_command", "my-project");

        await _sut.HandleAsync(payload, CancellationToken.None);

        _adapter.Verify(a => a.SendMessageAsync(
            "C123",
            It.Is<string>(s => s.Contains("Invalid command")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_FixBug_MissingTicket_SendsError()
    {
        var payload = BuildPayload("fix_bug", "my-project");

        await _sut.HandleAsync(payload, CancellationToken.None);

        _adapter.Verify(a => a.SendMessageAsync(
            "C123",
            It.Is<string>(s => s.Contains("select a ticket")),
            It.IsAny<CancellationToken>()), Times.Once);
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
