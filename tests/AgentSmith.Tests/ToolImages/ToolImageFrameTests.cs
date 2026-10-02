using AgentSmith.Contracts.Models;
using AgentSmith.Infrastructure.Models;
using AgentSmith.Infrastructure.Services.ToolImages;
using FluentAssertions;
using Microsoft.Extensions.AI;

namespace AgentSmith.Tests.ToolImages;

/// <summary>2026-10-01-283dd: the per-loop frame, its ambient holder and the deposit over it.</summary>
public sealed class ToolImageFrameTests
{
    [Fact]
    public void Add_ThirdImageOfOneCall_IsRefused()
    {
        var frame = new ToolImageLoopFrame();
        frame.Add(Deposited("c1")).Should().BeNull();
        frame.Add(Deposited("c1")).Should().BeNull();
        frame.Add(Deposited("c1")).Should().Contain("at most 2");
        frame.Add(Deposited("c2")).Should().BeNull();
    }

    [Fact]
    public void TakePending_HandsOutOnce()
    {
        var frame = new ToolImageLoopFrame();
        frame.Add(Deposited("c1"));
        frame.TakePending().Should().ContainSingle();
        frame.TakePending().Should().BeEmpty();
    }

    [Fact]
    public void ReserveShowings_GrantsAtMostFourPerLoop()
    {
        var frame = new ToolImageLoopFrame();
        frame.ReserveShowings(3).Should().Be(3);
        frame.ReserveShowings(3).Should().Be(1);
        frame.ReserveShowings(1).Should().Be(0);
    }

    [Fact]
    public void Open_NestedFrame_UnwindsToTheEnclosingOne()
    {
        var frames = new ToolImageLoopFrames();
        using (frames.Open())
        {
            var outer = frames.Current;
            using (frames.Open()) frames.Current.Should().NotBeSameAs(outer);
            frames.Current.Should().BeSameAs(outer);
        }
        frames.Current.Should().BeNull();
    }

    [Fact]
    public void Deposit_OutsideAToolCall_IsRefused()
    {
        var frames = new ToolImageLoopFrames();
        var deposit = new AsyncLocalToolImageDeposit(new ToolImageRule(new ImageDimensionReader()), frames);
        using var _ = frames.Open();

        var result = deposit.Deposit(new ToolImage("image/png", ToolImageLoopFixture.Png(10, 10), "x"));

        result.Should().Be(ToolImageDepositResult.Refused(AsyncLocalToolImageDeposit.NoLoopRefusal));
    }

    [Fact]
    public void Relay_FollowOutsideALoop_ReturnsTheToolMessagesUnchanged()
    {
        var relay = new ToolImageRelay(new ToolImageLoopFrames(), new ToolImageMessageComposer());
        IList<ChatMessage> tool = [new ChatMessage(ChatRole.Tool, [new FunctionResultContent("c1", "ok")])];

        relay.Follow(tool, ToolImageDelivery.For(true, true)).Should().BeSameAs(tool);
    }

    private static DepositedToolImage Deposited(string callId) =>
        new(new ToolImage("image/png", [1], "x"), "render", callId);
}
