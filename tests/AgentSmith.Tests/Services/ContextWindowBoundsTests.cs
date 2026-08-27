using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Infrastructure.Services.Providers.Agent;
using AgentSmith.Sandbox.Wire;
using FluentAssertions;
using Microsoft.Extensions.AI;

namespace AgentSmith.Tests.Services;

/// <summary>
/// 2026-08-27-3eb1: the three bounds the sweep was missing — one tool result, the
/// serialized middle handed to the summarizer, and the threshold a stated window implies.
/// </summary>
public sealed class ContextWindowBoundsTests
{
    [Fact]
    public void Bound_ShortResult_IsReturnedVerbatim() =>
        BoundedResultTool.Bound("hello", 100).Should().Be("hello");

    [Fact]
    public void Bound_OversizedResult_IsCutAndSaysWhatItCut()
    {
        var bounded = BoundedResultTool.Bound(new string('x', 500), 100);

        bounded.Should().StartWith(new string('x', 100));
        bounded.Should().Contain("400 of 500 characters omitted");
    }

    [Fact]
    public async Task Wrap_AnOversizedToolResult_IsBoundedOnTheScoutSurface()
    {
        var oversized = new string('y', SizeLimits.ExploringToolResultMaxChars * 5);
        var tool = (AIFunction)BoundedResultTool.Wrap(
            AIFunctionFactory.Create(() => oversized, "big"));

        var result = await tool.InvokeAsync(new AIFunctionArguments(), CancellationToken.None);

        var text = result!.ToString()!;
        text.Length.Should().BeLessThan(SizeLimits.ExploringToolResultMaxChars + 200);
        text.Should().StartWith(new string('y', SizeLimits.ExploringToolResultMaxChars));
        text.Should().Contain("truncated");
    }

    [Fact]
    public void Serialize_AnEnormousMiddle_StopsAtTheBudgetAndSaysSo()
    {
        var middle = Enumerable.Range(0, 400)
            .Select(i => new ChatMessage(ChatRole.Assistant, new string('z', 1000)))
            .ToList();

        var text = new CompactionSummaryRequest().Serialize(middle);

        text.Length.Should().BeLessThan(CompactionSummaryRequest.MaxSerializedChars + 2000);
        text.Should().Contain("of 400 messages omitted");
    }

    [Fact]
    public void Build_ProducesASystemInstructionAndTheRenderedMiddle()
    {
        var prompt = new CompactionSummaryRequest()
            .Build([new ChatMessage(ChatRole.Assistant, "read Foo.cs")]);

        prompt.Should().HaveCount(2);
        prompt[0].Role.Should().Be(ChatRole.System);
        prompt[0].Text.Should().Contain("context compactor");
        prompt[1].Text.Should().Contain("[Assistant] read Foo.cs");
    }

    [Fact]
    public void Derive_NoWindow_YieldsNothing() =>
        new WindowDerivedCompaction().Derive(new CompactionConfig(), null).Should().BeNull();

    [Fact]
    public void Derive_CompactionDisabled_YieldsNothing() =>
        new WindowDerivedCompaction()
            .Derive(new CompactionConfig { IsEnabled = false }, 128000).Should().BeNull();

    [Fact]
    public void Derive_AWindowSmallerThanTheThreshold_ClampsToTheWindow()
    {
        var derived = new WindowDerivedCompaction()
            .Derive(new CompactionConfig { MaxContextTokens = 200000 }, 128000);

        derived!.MaxContextTokens.Should().Be(128000);
    }

    [Fact]
    public void Derive_AnEarlierExplicitThreshold_IsKept()
    {
        var derived = new WindowDerivedCompaction()
            .Derive(new CompactionConfig { MaxContextTokens = 80000 }, 128000);

        derived!.MaxContextTokens.Should().Be(80000);
    }

    // The source object is the agent's shared config — deriving must not edit it.
    [Fact]
    public void Derive_LeavesTheRequestedConfigAlone()
    {
        var requested = new CompactionConfig { MaxContextTokens = 200000 };

        new WindowDerivedCompaction().Derive(requested, 128000);

        requested.MaxContextTokens.Should().Be(200000);
    }
}
