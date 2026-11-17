using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.TicketActs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.TicketActs;

/// <summary>2026-11-17-9a39c: the 'retarget' run's step moves a pull request still based on the landed branch, and only it.</summary>
public sealed class RetargetPullRequestsHandlerTests
{
    private const string Url = "https://git.test/o/api/pull/7";
    private readonly Mock<ISourceProvider> _provider = new();
    private readonly ResolvedProject _project = new() { Name = "alpha", Repos = [new RepoConnection { Name = "api" }] };

    [Fact]
    public async Task Retarget_APullRequestOnTheLandedBranch_MovesIt()
    {
        _provider.Setup(p => p.ReadPullRequestBaseAsync(Url, default)).ReturnsAsync("agent-smith/41");
        _provider.Setup(p => p.RetargetPullRequestAsync(Url, new BranchName("main"), default)).ReturnsAsync(true);

        var (result, slot) = await RunAsync();

        result.IsSuccess.Should().BeTrue();
        slot.Value.Should().Be(1);
        _provider.Verify(p => p.RetargetPullRequestAsync(Url, new BranchName("main"), default), Times.Once);
    }

    [Fact]
    public async Task Retarget_APullRequestSomeoneMovedMeanwhile_IsLeft()
    {
        _provider.Setup(p => p.ReadPullRequestBaseAsync(Url, default)).ReturnsAsync("release");

        var (result, slot) = await RunAsync();

        slot.Value.Should().Be(0);
        result.Message.Should().Contain("based on 'release'");
        _provider.Verify(p => p.RetargetPullRequestAsync(It.IsAny<string>(), It.IsAny<BranchName>(), default), Times.Never);
    }

    [Fact]
    public async Task Retarget_NoRequestSeeded_FailsNamingTheKey()
    {
        var result = await new RetargetPullRequestsHandler(Mock.Of<ISourceProviderFactory>())
            .ExecuteAsync(new RetargetPullRequestsContext(new PipelineContext(), _project), default);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain(nameof(ContextKeys.RetargetPullRequestsRequest));
    }

    private async Task<(CommandResult Result, ActSlot<int> Slot)> RunAsync()
    {
        var (pipeline, slot) = (new PipelineContext(), new ActSlot<int>());
        pipeline.Set(ContextKeys.RetargetPullRequestsRequest,
            new RetargetPullRequestsRequest("42", [new PullRequestMove("api", Url, "agent-smith/41", "main")]));
        pipeline.Set(ContextKeys.ActOutcome, slot);
        var sources = new Mock<ISourceProviderFactory>();
        sources.Setup(s => s.Create(It.IsAny<RepoConnection>())).Returns(_provider.Object);
        return (await new RetargetPullRequestsHandler(sources.Object).ExecuteAsync(new RetargetPullRequestsContext(pipeline, _project), default), slot);
    }
}
