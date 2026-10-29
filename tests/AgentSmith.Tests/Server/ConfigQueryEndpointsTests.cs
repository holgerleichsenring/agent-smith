using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Server.Extensions;
using AgentSmith.Server.Services.Config;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Moq;

namespace AgentSmith.Tests.Server;

/// <summary>
/// 2026-10-29-6bd3d: GET /api/config answers the configuration as it is now. It bound the
/// AgentSmithConfig singleton, built once at boot, so a tracker saved in the studio never showed.
/// </summary>
public sealed class ConfigQueryEndpointsTests
{
    [Fact]
    public void Snapshot_ATrackerStoredAfterBoot_IsInTheSnapshot()
    {
        var current = new AgentSmithConfig
        {
            Trackers = new() { ["added-later"] = new TrackerConnection { Name = "added-later", Type = TrackerType.Jira } },
        };
        var loader = new Mock<IConfigurationLoader>();
        loader.Setup(l => l.LoadConfig("db://config-entity")).Returns(current);

        var result = ConfigQueryEndpoints.Snapshot(
            loader.Object, new ServerContext("db://config-entity"),
            Microsoft.Extensions.Options.Options.Create(new SandboxGlobalConfig()),
            new AgentSmith.Tests.Sandbox.StubSandboxResourceResolver(),
            new AgentSmith.Tests.Sandbox.StubAgentImageResolver());

        var snapshot = (ConfigSnapshot)((IValueHttpResult)result).Value!;
        snapshot.Trackers.Select(t => t.Name).Should().Equal("added-later");
    }

    [Fact]
    public void Snapshot_TheLastRead_IsTheOneBeforeThisRequestsOwn()
    {
        var before = new ConfigFileReadFact("db://config-entity", DateTimeOffset.Parse("2026-10-29T08:00:00Z"));
        var lastRead = before;
        var loader = new Mock<IConfigurationLoader>();
        loader.SetupGet(l => l.LastRead).Returns(() => lastRead);
        loader.Setup(l => l.LoadConfig(It.IsAny<string>()))
            .Callback(() => lastRead = before with { ReadAt = DateTimeOffset.UtcNow })
            .Returns(new AgentSmithConfig());

        var result = ConfigQueryEndpoints.Snapshot(
            loader.Object, new ServerContext("db://config-entity"),
            Microsoft.Extensions.Options.Options.Create(new SandboxGlobalConfig()),
            new AgentSmith.Tests.Sandbox.StubSandboxResourceResolver(),
            new AgentSmith.Tests.Sandbox.StubAgentImageResolver());

        ((ConfigSnapshot)((IValueHttpResult)result).Value!).LastReadAt.Should().Be(before.ReadAt,
            "the request's own read would always say 'read just now'");
    }
}
