using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using static AgentSmith.Tests.TestSupport.TicketRequirementEdges;

namespace AgentSmith.Tests.Persistence;

/// <summary>
/// 2026-10-14-dbcef: two replicas filing one dependent's edges meet at the unique index. The loser's
/// whole save rolls back, so it re-reads and inserts what is still missing — no edge is left unwritten;
/// a second violation is a real fault and propagates.
/// </summary>
public sealed class TicketRequirementRaceTests
{
    [Fact]
    public async Task TicketRequirementStore_TwoRacingWritersWithOverlappingUnequalBatches_StoreEveryEdgeOfBothOnce()
    {
        var fired = false;
        ServerStateStore? store = null;
        using var racing = new ServerStateStore(unitOfWork: db => new RacingUnitOfWork(db, async () =>
        {
            if (fired) return;
            fired = true;
            (await new TicketRequirementRepository(store!.ScopeFactory)
                .AddAsync([Edge("a-4", "a-1"), Edge("a-4", "a-2")], default)).Should().Be(2);
        }));
        store = racing;

        var added = await new TicketRequirementRepository(racing.ScopeFactory)
            .AddAsync([Edge("a-4", "a-2"), Edge("a-4", "a-3")], default);

        fired.Should().BeTrue();
        added.Should().Be(1, "the other writer stored a-2; this call added a-3 after its first save rolled back");
        await using var db = racing.Context();
        (await db.Set<TicketRequirement>().Select(r => r.RequiredTicketKey).OrderBy(k => k).ToListAsync())
            .Should().Equal("jira-a-1", "jira-a-2", "jira-a-3");
    }

    [Fact]
    public async Task TicketRequirementStore_SecondUniqueViolation_Throws()
    {
        var pending = new Queue<string>(["a-2", "a-3"]);
        ServerStateStore? store = null;
        using var racing = new ServerStateStore(unitOfWork: db => new RacingUnitOfWork(db, async () =>
        {
            if (pending.Count == 0) return;
            await using var other = store!.Context();
            var edge = Edge("a-4", pending.Dequeue());
            other.Add(new TicketRequirement
            {
                Tracker = edge.Tracker, TicketKey = edge.TicketKey, TicketId = edge.TicketId, Project = edge.Project,
                RequiredTicketKey = edge.RequiredTicketKey, RequiredTicketId = edge.RequiredTicketId,
            });
            await other.SaveChangesAsync();
        }));
        store = racing;

        var add = () => new TicketRequirementRepository(racing.ScopeFactory)
            .AddAsync([Edge("a-4", "a-1"), Edge("a-4", "a-2"), Edge("a-4", "a-3")], default);

        await add.Should().ThrowAsync<DbUpdateException>();
        pending.Should().BeEmpty("the race fired before both saves");
    }
}
