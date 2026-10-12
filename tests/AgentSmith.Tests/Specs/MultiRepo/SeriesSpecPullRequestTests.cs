using AgentSmith.Application.Services.Resume;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Specs.MultiRepo;

/// <summary>
/// 2026-10-12-f5f7e: every series repository gets its own draft spec pull request; the URLs live in one
/// map, and a run parked before the map existed still finds its spec pull request on resume.
/// </summary>
public sealed class SeriesSpecPullRequestTests
{
    private static readonly RepoConnection Server = new() { Name = "Server", Url = "https://x/server.git" };
    private static readonly RepoConnection Client = new() { Name = "Client", Url = "https://x/client.git" };
    private readonly Mock<ISourceProvider> _provider = new();

    public SeriesSpecPullRequestTests() =>
        _provider.Setup(p => p.CreatePullRequestAsync(It.IsAny<Repository>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>(), It.IsAny<TicketId?>(), It.IsAny<bool>(), It.IsAny<BranchName?>()))
            .ReturnsAsync((Repository r, string _, string _, CancellationToken _, TicketId? _, bool _, BranchName? _) => $"{r.RemoteUrl}/pull/1");

    [Fact]
    public async Task SpecPullRequestOpener_TwoSeriesRepos_OpensTwoDraftPrs()
    {
        var set = SeriesPerRepoWriteTests.Set(("a", "Server"), ("b", "Client"));

        var urls = await Opener().OpenAsync(Pipeline(), new SeriesPlacement(set, [Server, Client], Server),
            new SeriesScope([Server, Client], null, [], null), CancellationToken.None);

        urls.Should().BeEquivalentTo(new Dictionary<string, string>
        {
            ["Server"] = "https://x/server.git/pull/1", ["Client"] = "https://x/client.git/pull/1",
        });
        _provider.Verify(p => p.CreatePullRequestAsync(It.IsAny<Repository>(), It.IsAny<string>(),
            It.Is<string>(body => body.Contains(SpecPrBody.SiblingMarker)), It.IsAny<CancellationToken>(),
            It.IsAny<TicketId?>(), true, It.IsAny<BranchName?>()), Times.Exactly(2));
    }

    [Fact]
    public async Task SpecPullRequestOpener_RepoDroppedFromTheSeries_IsFoundNotOpened()
    {
        _provider.Setup(p => p.FindOpenPullRequestAsync(
                It.Is<Repository>(r => r.RemoteUrl == Client.Url), It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://x/client.git/pull/9");
        var set = SeriesPerRepoWriteTests.Set(("a", "Server"));

        var urls = await Opener().OpenAsync(Pipeline(), new SeriesPlacement(set, [Server], Server),
            new SeriesScope([Server, Client], null, ["Server", "Client"], "Server"), CancellationToken.None);

        urls["Client"].Should().Be("https://x/client.git/pull/9", "the comment lists it as dropped");
        _provider.Verify(p => p.CreatePullRequestAsync(It.Is<Repository>(r => r.RemoteUrl == Client.Url),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<TicketId?>(),
            It.IsAny<bool>(), It.IsAny<BranchName?>()), Times.Never);
    }

    [Fact]
    public void SpecPullRequests_Record_KeepsEveryOtherRepository()
    {
        var pipeline = new PipelineContext();

        SpecPullRequests.Record(pipeline, "Server", "https://x/server/pull/1");
        SpecPullRequests.Record(pipeline, "Client", "https://x/client/pull/2");

        SpecPullRequests.Of(pipeline).Should().HaveCount(2).And.ContainKey("Server").And.ContainKey("Client");
    }

    [Fact]
    public void Resume_LegacySpecPullRequestUrl_ReadAsMap()
    {
        var parked = new PipelineContext();
        parked.Set(ContextKeys.SpecRepo, "Server");
        parked.Set(ContextKeys.SpecPullRequestUrl, "https://x/server/pull/1");

        var resumed = RoundTrip(parked);

        SpecPullRequests.Of(resumed).Should().BeEquivalentTo(
            new Dictionary<string, string> { ["Server"] = "https://x/server/pull/1" });
    }

    [Fact]
    public void Resume_NewMapKey_RoundTrips()
    {
        var parked = new PipelineContext();
        SpecPullRequests.Record(parked, "Server", "https://x/server/pull/1");
        SpecPullRequests.Record(parked, "Client", "https://x/client/pull/2");

        var resumed = RoundTrip(parked);

        SpecPullRequests.Of(resumed).Should().BeEquivalentTo(SpecPullRequests.Of(parked));
    }

    private static PipelineContext RoundTrip(PipelineContext parked)
    {
        var serializer = new PipelineContextSerializer(NullLogger<PipelineContextSerializer>.Instance);
        var resumed = new PipelineContext();
        serializer.Restore(serializer.Serialize(parked), resumed);
        return resumed;
    }

    private SpecPullRequestOpener Opener()
    {
        var factory = new Mock<ISourceProviderFactory>();
        factory.Setup(f => f.Create(It.IsAny<RepoConnection>())).Returns(_provider.Object);
        return new SpecPullRequestOpener(factory.Object, EventTestStubs.NoOp, NullLogger<SpecPullRequestOpener>.Instance);
    }

    private static PipelineContext Pipeline()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.Repository, new Repository(new BranchName("agent-smith/42"), "https://x/primary.git"));
        return pipeline;
    }
}
