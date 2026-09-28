using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services;
using AgentSmith.Server.Services.Adapters;
using AgentSmith.Server.Services.Handlers;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Dispatcher;

/// <summary>
/// A reply answers on the platform the message came from. These handlers took a single
/// IPlatformAdapter, which resolves to the last registered adapter — Teams — so a Slack
/// command was answered through the Teams adapter. Teams is registered last here on purpose.
/// </summary>
public sealed class ChatRepliesFollowPlatformTests
{
    private readonly RecordingPlatformAdapter _slack = new(DispatcherDefaults.PlatformSlack);
    private readonly RecordingPlatformAdapter _teams = new(DispatcherDefaults.PlatformTeams);
    private readonly Mock<IConfigurationLoader> _loader = new();

    public ChatRepliesFollowPlatformTests() =>
        _loader.Setup(l => l.LoadConfig(It.IsAny<string>())).Returns(new AgentSmithConfig());

    private PlatformAdapters Adapters() => new([_slack, _teams], NullLogger<PlatformAdapters>.Instance);

    [Fact]
    public async Task HelpHandler_SlackIntent_RepliesThroughSlack()
    {
        var help = new HelpHandler(Adapters(), NullLogger<HelpHandler>.Instance);

        await help.SendHelpAsync(DispatcherDefaults.PlatformSlack, "C1", default);
        await help.SendClarificationAsync(DispatcherDefaults.PlatformSlack, "C1", "fix #1", default);

        _slack.Sent.Should().HaveCount(2);
        _teams.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task ListTicketsIntentHandler_SlackIntent_RepliesThroughSlack()
    {
        var handler = new ListTicketsIntentHandler(
            Adapters(), _loader.Object, Mock.Of<ITicketProviderFactory>(),
            NullLogger<ListTicketsIntentHandler>.Instance);

        await handler.HandleAsync(new ListTicketsIntent
        {
            RawText = "list tickets in nowhere", UserId = "U1", ChannelId = "C1",
            Platform = DispatcherDefaults.PlatformSlack, Project = "nowhere",
        }, default);

        _slack.Sent.Should().ContainSingle().Which.Should().Contain("nowhere");
        _teams.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateTicketIntentHandler_SlackIntent_RepliesThroughSlack()
    {
        var handler = new CreateTicketIntentHandler(
            Adapters(), _loader.Object, Mock.Of<ITicketProviderFactory>(),
            TestSupport.ApprovedSetDoubles.Kinds(), NullLogger<CreateTicketIntentHandler>.Instance);

        await handler.HandleAsync(new CreateTicketIntent
        {
            RawText = "create ticket", UserId = "U1", ChannelId = "C1",
            Platform = DispatcherDefaults.PlatformSlack, Project = "nowhere", Title = "Add logging",
        }, default);

        _slack.Sent.Should().ContainSingle().Which.Should().Contain("nowhere");
        _teams.Sent.Should().BeEmpty();
    }
}
