using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using static AgentSmith.Tests.TestSupport.TicketRequirementEdges;

namespace AgentSmith.Tests.Persistence;

/// <summary>
/// 2026-10-14-dbcef: a requirement between two tickets is stored once per edge, over the shipped
/// migrations, and read back by the dependent, by the required ticket and by project; a batch that
/// names two dependents, a ticket requiring itself or two answers to StartsWhenMet is refused.
/// </summary>
public sealed class TicketRequirementStoreTests : IDisposable
{
    private readonly ServerStateStore _store = new();

    private TicketRequirementRepository Store => new(_store.ScopeFactory);

    [Fact]
    public async Task TicketRequirementStore_AddThenRequiredOf_ReturnsThatTicketsEdgesOnly()
    {
        (await Store.AddAsync([Edge("a-2", "a-1"), Edge("a-2", "a-0")], default)).Should().Be(2);
        await Store.AddAsync([Edge("a-3", "a-1")], default);

        var edges = await Store.RequiredOfAsync(Tracker, "jira-a-2", default);

        edges.Select(e => e.RequiredTicketKey).Should().Equal("jira-a-1", "jira-a-0");
        edges.Should().AllSatisfy(e => e.RequiredTicketId.Should().StartWith("A-"));
        (await Store.RequiredOfAsync(Tracker, "jira-unknown", default)).Should().BeEmpty();
    }

    [Fact]
    public async Task TicketRequirementStore_DependentsOf_ReturnsEveryTicketRequiringTheKey()
    {
        await Store.AddAsync([Edge("a-2", "a-1")], default);
        await Store.AddAsync([Edge("a-3", "a-1"), Edge("a-3", "a-2")], default);

        var dependents = await Store.DependentsOfAsync(Tracker, "jira-a-1", default);

        dependents.Select(e => e.TicketKey).Should().Equal("jira-a-2", "jira-a-3");
        (await Store.DependentsOfAsync(Tracker, "jira-a-3", default)).Should().BeEmpty();
    }

    [Fact]
    public async Task TicketRequirementStore_ByProject_ReturnsOnlyThatProjectsEdges()
    {
        await Store.AddAsync([Edge("a-2", "a-1")], default);
        await Store.AddAsync([Edge("b-2", "b-1", project: "other")], default);

        (await Store.ByProjectAsync(Project, default)).Should().ContainSingle().Which.TicketKey.Should().Be("jira-a-2");
        (await Store.ByProjectAsync("nobody", default)).Should().BeEmpty();
    }

    [Fact]
    public async Task TicketRequirementStore_SameEdgeTwice_StoresOneRowAndReportsNoneAdded()
    {
        await Store.AddAsync([Edge("a-2", "a-1")], default);

        (await Store.AddAsync([Edge("a-2", "a-1")], default)).Should().Be(0);

        (await Store.RequiredOfAsync(Tracker, "jira-a-2", default)).Should().ContainSingle();
    }

    [Fact]
    public async Task TicketRequirementStore_BatchWithMixedStartsWhenMet_IsRefused()
    {
        var add = () => Store.AddAsync([Edge("a-3", "a-1", startsWhenMet: true), Edge("a-3", "a-2")], default);

        await add.Should().ThrowAsync<ArgumentException>();
        (await Store.RequiredOfAsync(Tracker, "jira-a-3", default)).Should().BeEmpty();
    }

    [Fact]
    public async Task TicketRequirementStore_EdgeRequiringItsOwnTicket_IsRefused()
    {
        var add = () => Store.AddAsync([Edge("a-1", "a-1")], default);

        await add.Should().ThrowAsync<ArgumentException>().WithMessage("*cannot require itself*");
    }

    [Fact]
    public async Task TicketRequirementStore_BatchNamingTwoDependents_IsRefused()
    {
        var add = () => Store.AddAsync([Edge("a-2", "a-1"), Edge("a-3", "a-1")], default);

        await add.Should().ThrowAsync<ArgumentException>().WithMessage("*one dependent ticket*");
    }

    [Fact]
    public async Task TicketRequirementStore_SameTicketKeyOnTwoTrackerConnections_AreTwoTickets()
    {
        await Store.AddAsync([Edge("a-2", "a-1")], default);

        (await Store.AddAsync([Edge("a-2", "a-1", tracker: "second-tracker")], default)).Should().Be(1);

        (await Store.RequiredOfAsync("second-tracker", "jira-a-2", default)).Should().ContainSingle()
            .Which.Tracker.Should().Be("second-tracker");
        (await Store.DependentsOfAsync(Tracker, "jira-a-1", default)).Should().ContainSingle();
    }

    [Fact]
    public async Task TicketRequirementStore_StartsWhenMet_RoundTrips()
    {
        await Store.AddAsync([Edge("a-2", "a-1", startsWhenMet: true)], default);
        await Store.AddAsync([Edge("a-3", "a-1")], default);

        (await Store.DependentsOfAsync(Tracker, "jira-a-1", default)).Select(e => (e.TicketKey, e.StartsWhenMet))
            .Should().Equal(("jira-a-2", true), ("jira-a-3", false));
    }

    [Fact]
    public async Task TicketRequirementStore_FiledAt_IsTheRowsInsertTime()
    {
        var before = DateTimeOffset.UtcNow;
        await Store.AddAsync([Edge("a-2", "a-1") with { FiledAt = new DateTimeOffset(2001, 1, 1, 0, 0, 0, TimeSpan.Zero) }], default);
        var after = DateTimeOffset.UtcNow;

        var edge = (await Store.RequiredOfAsync(Tracker, "jira-a-2", default)).Single();

        edge.FiledAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after, "the edge's FiledAt is the row's CreatedAt");
        edge.FiledInConversation.Should().Be("conversation-1");
    }

    [Fact]
    public async Task TicketRequirementStore_DeletingEveryRunOfBothTickets_LeavesTheEdge()
    {
        await Store.AddAsync([Edge("a-2", "a-1")], default);
        await using (var db = _store.Context())
        {
            db.Runs.AddRange(
                new Run { Id = "run-1", Project = Project, Pipeline = "fix-bug", TicketId = "A-1" },
                new Run { Id = "run-2", Project = Project, Pipeline = "fix-bug", TicketId = "A-2" });
            await db.SaveChangesAsync();
        }

        await using (var db = _store.Context())
            foreach (var run in new[] { "run-1", "run-2" }) await new RunDeletionRepository(db).DeleteAsync(run, default);

        await using (var db = _store.Context()) db.Runs.Should().BeEmpty();
        (await Store.RequiredOfAsync(Tracker, "jira-a-2", default)).Should().ContainSingle();
    }

    public void Dispose() => _store.Dispose();
}
