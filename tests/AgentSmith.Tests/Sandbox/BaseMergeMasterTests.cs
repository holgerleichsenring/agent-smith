using AgentSmith.Application.Services;
using AgentSmith.Application.Services.BaseRefresh;
using AgentSmith.Application.Services.Loop;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Moq;

namespace AgentSmith.Tests.Sandbox;

/// <summary>2026-11-24-9dc0a: the base-merge master's passes are one conversation — a continuation carries the earlier
/// turn and answer, a conclude-only ask offers conclude alone, and every pass's cost is the run's.</summary>
public sealed class BaseMergeMasterTests
{
    private readonly List<AgenticLoopRequest> _requests = [];

    [Fact]
    public async Task ContinueAsync_ConcludeOnly_SendsConcludeAlone_AndTheEarlierThread()
    {
        var (master, situation) = Master();

        var first = await master.RunAsync(situation, CancellationToken.None);
        var asked = await master.ContinueAsync(first, "Say why.", concludeOnly: true, CancellationToken.None);

        first.Whole.Should().BeNull("the loop ended without conclude");
        _requests.Should().HaveCount(2);
        _requests[0].Tools.Should().Contain(t => t.Name == "conclude").And.HaveCountGreaterThan(1);
        _requests[1].Tools.Should().ContainSingle().Which.Name.Should().Be("conclude");
        _requests[1].UserPrompt.Should().Be("Say why.");
        _requests[1].PriorMessages!.Select(m => m.Text).Should().Equal(BaseMergeMaster.Prompt(situation), "answer 1");
        asked.Session.Should().BeSameAs(first.Session);
        PipelineCostTracker.GetOrCreate(situation.Pipeline).CallCount.Should().Be(2, "each pass's cost is tracked");
    }

    [Fact]
    public async Task ContinueAsync_WithTools_OffersTheFileToolsAgain()
    {
        var (master, situation) = Master();

        var first = await master.RunAsync(situation, CancellationToken.None);
        await master.ContinueAsync(first, "Go on.", concludeOnly: false, CancellationToken.None);

        _requests[1].Tools.Select(t => t.Name).Should().BeEquivalentTo(_requests[0].Tools.Select(t => t.Name));
    }

    [Fact]
    public async Task ContinueAsync_APassNoMasterRan_Throws()
    {
        var (master, _) = Master();

        var act = () => master.ContinueAsync(new BaseMergePass(null, null, []), "x", true, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    private (BaseMergeMaster Master, BaseMergeSituation Situation) Master()
    {
        var loop = new Mock<IAgenticLoopRunner>();
        loop.Setup(l => l.RunAsync(It.IsAny<AgenticLoopRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AgenticLoopRequest r, CancellationToken _) =>
            {
                _requests.Add(r);
                return new AgenticLoopResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, $"answer {_requests.Count}"))
                    { Usage = new UsageDetails { InputTokenCount = 10, OutputTokenCount = 1 } }, TimeSpan.Zero);
            });
        var prompts = new Mock<IPromptCatalog>();
        prompts.Setup(p => p.Get(It.IsAny<string>())).Returns("system");
        var situation = new BaseMergeSituation(new ResolvedProject { Name = "p" }, new PipelineContext(), Mock.Of<ISandbox>(),
            "agent-smith/1", "origin/main", [], []) { Unmerged = ["a.txt"] };
        return (new BaseMergeMaster(loop.Object, prompts.Object, new BaseMergeWholeness(new BaseRefreshGit())), situation);
    }
}
