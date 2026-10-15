using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.Tickets;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using static AgentSmith.Tests.Tickets.TicketStoreHarness;

namespace AgentSmith.Tests.Tickets;

/// <summary>2026-10-13-9a59a: the list's cursor, counts, month cost, ceiling, keys and hand-back marks.</summary>
[Collection(RelationalStoreCollection.Name)]
public sealed class TicketListPagingTests : IDisposable
{
    private readonly TicketStoreHarness _store = new();

    public void Dispose() => _store.Dispose();

    [Fact]
    public async Task TicketList_CursorPage_NeverRepeatsATicket()
    {
        for (var i = 1; i <= 5; i++) await _store.AddAsync(Run(Id(i), ticket: $"{i}"), Step(Id(i)));

        var first = await ListAsync(new TicketListRequest(ProjectScope.Everything, null, null, 2));
        var second = await ListAsync(new TicketListRequest(ProjectScope.Everything, null, first.NextCursor, 2));
        var third = await ListAsync(new TicketListRequest(ProjectScope.Everything, null, second.NextCursor, 2));

        var seen = first.Tickets.Concat(second.Tickets).Concat(third.Tickets).Select(t => t.TicketId).ToList();
        seen.Should().Equal("5", "4", "3", "2", "1");
        third.NextCursor.Should().BeNull();
    }

    [Fact]
    public async Task TicketList_Counts_CoverEveryBucketIndependentOfThePage()
    {
        await _store.AddAsync(
            Run(Id(1), ticket: "1", status: "running"), Step(Id(1)),
            Run(Id(2), ticket: "2", status: "waiting_for_input"), Step(Id(2)),
            Run(Id(3), ticket: "3", status: "success"), Step(Id(3)));

        var result = await ListAsync(new TicketListRequest(ProjectScope.Everything, TicketBucket.Idle, null, 1));

        result.Tickets.Should().ContainSingle().Which.TicketId.Should().Be("3");
        result.Counts.Should().BeEquivalentTo(new { NeedsYou = 1, Working = 1, Running = 1, Queued = 0, Idle = 1 });
    }

    [Fact]
    public async Task TicketList_Counts_NameTheOldestNeedsYouSince()
    {
        await _store.AddAsync(
            Run(Id(1), ticket: "1", status: "waiting_for_input"), Step(Id(1)), Checkpoint(Id(1), "1"),
            Run(Id(2), ticket: "2", status: "waiting_for_input"), Step(Id(2)));

        (await ListAsync()).Counts.OldestNeedsYouSince.Should().Be(T.AddMinutes(10));
    }

    [Fact]
    public async Task TicketList_MonthCost_SumsEveryRunOfTheMonth()
    {
        await _store.AddAsync(
            Run(Id(1), ticket: "1", cost: 1.25m), Run(Id(2), ticket: "2", pipeline: "pr-review", cost: 0.50m),
            Run("2026-09-30T23-00-00-0009", ticket: "3", cost: 7m));

        (await ListAsync()).Counts.MonthCost.Should().Be(1.75m);
    }

    [Fact]
    public async Task TicketList_ProjectFilter_ScopesCountsAndMonthCost()
    {
        await _store.AddAsync(
            Run(Id(1), ticket: "1", cost: 1m), Run(Id(2), ticket: "2", project: "beta", cost: 2m, status: "running"));

        var result = await ListAsync(new TicketListRequest(ProjectScope.Single("alpha"), null, null, null));

        result.Tickets.Should().ContainSingle().Which.Project.Should().Be("alpha");
        result.Counts.MonthCost.Should().Be(1m);
        result.Counts.Working.Should().Be(0);
    }

    [Fact]
    public async Task TicketList_ScopedCaller_ListsAndCountsOnlyTheirProjects()
    {
        await _store.AddAsync(
            Run(Id(1), ticket: "1", cost: 1m), Run(Id(2), ticket: "2", project: "beta", cost: 2m, status: "running"),
            Run(Id(3), ticket: "3", project: "gamma", cost: 4m));

        var result = await ListAsync(new TicketListRequest(ProjectScope.Of(["alpha", "gamma"]), null, null, null));

        result.Tickets.Select(t => t.Project).Should().BeEquivalentTo(["alpha", "gamma"]);
        result.Counts.MonthCost.Should().Be(5m, "beta's cost is not the caller's to see");
        result.Counts.Working.Should().Be(0);
    }

    [Fact]
    public async Task TicketList_ScopedCallerAskingForAnotherProject_IsTheEmptyList()
    {
        await _store.AddAsync(Run(Id(1), ticket: "1", cost: 1m), Run(Id(2), ticket: "2", project: "beta", cost: 2m));

        var foreign = await ListAsync(new TicketListRequest(ProjectScope.Of(["alpha"]).Narrowed("beta"), null, null, null));
        var unknown = await ListAsync(new TicketListRequest(ProjectScope.Everything.Narrowed("nowhere"), null, null, null));

        foreign.Tickets.Should().BeEmpty();
        foreign.Counts.Should().BeEquivalentTo(unknown.Counts);
    }

    [Fact]
    public async Task TicketList_CeilingReached_SaysTruncated()
    {
        var rows = Enumerable.Range(1, TicketListQuery.Ceiling + 1)
            .Select(i => (object)Run($"2026-10-13T09-00-00-{i:x5}", ticket: $"{i}")).ToArray();
        await _store.AddAsync(rows);

        (await ListAsync(new TicketListRequest(ProjectScope.Everything, null, null, 1))).Counts.Truncated.Should().BeTrue();
    }

    [Fact]
    public async Task TicketList_Row_CarriesTheConversationTicketKey()
    {
        await _store.AddAsync(Run(Id(1), ticket: "PROJ-7", platform: "jira"));

        (await ListAsync()).Tickets.Single().TicketKey.Should().Be("jira-proj-7");
    }

    [Fact]
    public async Task TicketList_RemovedProject_KeyFromRunPlatform()
    {
        await _store.AddAsync(Run(Id(1), ticket: "9", project: "gone", platform: "azuredevops"),
            Run(Id(2), ticket: "8", platform: null));

        var keys = (await ListAsync()).Tickets.ToDictionary(t => t.TicketId, t => t.TicketKey);

        keys["9"].Should().Be("azuredevops-9");
        keys["8"].Should().Be("github-8", "a run without a platform falls back to the configured tracker");
    }

    [Fact]
    public async Task TicketList_HandedBackSpec_MarksTheAttempt()
    {
        await _store.AddAsync(Run(Id(1), status: "failed"), Step(Id(1)), Spec(Id(1), "s1", "handed_back"),
            Run(Id(2), status: "success"), Step(Id(2)), Spec(Id(2), "s1", "done"));

        var row = (await ListAsync()).Tickets.Single();

        row.Attempts.Select(a => a.HandedBack).Should().Equal(true, false);
        (row.SpecsDone, row.SpecsTotal).Should().Be((1, 1));
    }

    [Fact]
    public async Task TicketList_DerivationHandback_MarksTheAttempt()
    {
        await _store.AddAsync(Run(Id(1), status: "waiting_for_input"), Step(Id(1)));

        (await ListAsync()).Tickets.Single().Attempts.Single().HandedBack.Should().BeTrue();
    }

    private static string Id(int i) => $"2026-10-13T09-00-0{i}-000{i}";

    private Task<TicketListView> ListAsync(TicketListRequest? request = null) =>
        _store.List.ReadAsync(request ?? new TicketListRequest(ProjectScope.Everything, null, null, null), CancellationToken.None);
}
