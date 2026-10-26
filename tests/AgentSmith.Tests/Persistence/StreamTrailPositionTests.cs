using AgentSmith.Infrastructure.Persistence.Services;
using FluentAssertions;

namespace AgentSmith.Tests.Persistence;

/// <summary>
/// 2026-10-26-0f19a: a trail position read off a Redis stream entry id — the same number on every
/// replica, ordered like the stream, below the dashboard's 2^53 and above every counted position.
/// </summary>
public sealed class StreamTrailPositionTests
{
    [Fact]
    public void Of_EntriesInStreamOrder_AreOrderedTheSameWay()
    {
        var ids = new[] { "1760000000000-0", "1760000000000-1", "1760000000000-999", "1760000000001-0" };

        ids.Select(StreamTrailPosition.Of).Should().BeInAscendingOrder().And.OnlyHaveUniqueItems();
    }

    [Fact]
    public void Of_ThenToEntryId_GivesTheEntryBack() =>
        StreamTrailPosition.ToEntryId(StreamTrailPosition.Of("1760000000123-42")).Should().Be("1760000000123-42");

    [Fact]
    public void Of_AnyEntryAfter2001_SitsAboveTheFloorAndBelowTheDashboardCeiling()
    {
        StreamTrailPosition.Of("1000000000000-0").Should().BeGreaterThanOrEqualTo(StreamTrailPosition.Floor);
        StreamTrailPosition.Of("9000000000000-999").Should().BeLessThan(1L << 53);
    }

    [Fact]
    public void Of_NotAnEntryId_Throws() =>
        FluentActions.Invoking(() => StreamTrailPosition.Of("tail")).Should().Throw<FormatException>();
}
