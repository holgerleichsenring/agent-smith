using AgentSmith.Contracts.Models.Compaction;
using FluentAssertions;

namespace AgentSmith.Tests.Compaction;

public sealed class CompactionEventTests
{
    [Fact]
    public void WithVerifiedTokens_ReturnsNewInstanceWithFieldPopulated()
    {
        var pending = new CompactionEvent(
            OldMessageCount: 24, NewMessageCount: 3,
            PreCompactionEstimatedTokens: 134_000,
            PostCompactionVerifiedTokens: null,
            SummarizerInputTokens: 1_000,
            SummarizerOutputTokens: 200,
            PromptHash: "abc12345",
            Failed: false, FailureReason: null);

        var finalized = pending.WithVerifiedTokens(18_000);

        finalized.PostCompactionVerifiedTokens.Should().Be(18_000);
        finalized.OldMessageCount.Should().Be(24);
        finalized.PromptHash.Should().Be("abc12345");
        // Original unchanged (record immutability)
        pending.PostCompactionVerifiedTokens.Should().BeNull();
    }

    [Fact]
    public void VerifiedSavedTokens_PendingVerified_ReturnsNull()
    {
        var pending = new CompactionEvent(0, 0, 100, null, 0, 0, "h", false, null);
        pending.VerifiedSavedTokens.Should().BeNull();
    }

    [Fact]
    public void VerifiedSavedTokens_AfterFinalization_ComputesDifference()
    {
        var finalized = new CompactionEvent(0, 0, 134_000, 18_000, 1_000, 200, "h", false, null);
        finalized.VerifiedSavedTokens.Should().Be(116_000);
    }

    [Fact]
    public void SummarizerTotalTokens_SumsInputAndOutput()
    {
        var ev = new CompactionEvent(0, 0, 0, null, 1_000, 200, "h", false, null);
        ev.SummarizerTotalTokens.Should().Be(1_200);
    }

    [Fact]
    public void ForFailure_ProducesEventWithFailedTrueAndReason()
    {
        var ev = CompactionEvent.ForFailure(oldCount: 12, estimatedTokens: 50_000, promptHash: "xyz", reason: "summarizer 429");

        ev.Failed.Should().BeTrue();
        ev.FailureReason.Should().Be("summarizer 429");
        ev.OldMessageCount.Should().Be(12);
        ev.NewMessageCount.Should().Be(12); // unchanged on failure
        ev.PreCompactionEstimatedTokens.Should().Be(50_000);
        ev.PostCompactionVerifiedTokens.Should().BeNull();
        ev.SummarizerInputTokens.Should().Be(0);
        ev.SummarizerOutputTokens.Should().Be(0);
        ev.PromptHash.Should().Be("xyz");
    }
}
