using AgentSmith.Server.Services.Tickets.Checks;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;

namespace AgentSmith.Tests.Tickets.Checks;

/// <summary>2026-11-23-1e76a: a ticket is marked kicked only once its kick is queued, so a full queue silences nobody.</summary>
public sealed class TicketCheckKicksTests
{
    private readonly TicketCheckKicks _kicks = new(new MemoryCache(new MemoryCacheOptions()));

    [Fact]
    public void Offer_QueueFull_TicketIsKickedOnceThereIsRoom()
    {
        for (var page = 0; page < 10; page++)
            _kicks.Offer(Enumerable.Range(page * 10, 10).Select(i => new TicketCheckKick("alpha", $"{i}")));

        _kicks.Offer([new TicketCheckKick("alpha", "late")]).Should().Be(0, "the queue holds a hundred");
        _kicks.Reader.TryRead(out _).Should().BeTrue();

        _kicks.Offer([new TicketCheckKick("alpha", "late")]).Should().Be(1);
    }

    [Fact]
    public void Offer_SameTicketTwice_QueuedOnce()
    {
        _kicks.Offer([new TicketCheckKick("alpha", "42")]);

        _kicks.Offer([new TicketCheckKick("alpha", "42")]).Should().Be(0);
        _kicks.Reader.Count.Should().Be(1);
    }
}
