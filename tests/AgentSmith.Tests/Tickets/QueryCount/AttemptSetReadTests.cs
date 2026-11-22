using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using static AgentSmith.Tests.Tickets.NeverRun.NeverRunFixtures;
using static AgentSmith.Tests.Tickets.TicketStoreHarness;

namespace AgentSmith.Tests.Tickets.QueryCount;

/// <summary>
/// 2026-11-22-9719d: the set reads over the real store answer what the single reads answer — which tickets have an
/// attempt, each one's newest attempt end, and each dependent's requirement edges.
/// </summary>
public sealed class AttemptSetReadTests : IDisposable
{
    private readonly TicketStoreHarness _h = new();

    public void Dispose() => _h.Dispose();

    private IPreviousAttemptReader Attempts => _h.Services.GetRequiredService<IPreviousAttemptReader>();

    private async Task SeedAsync()
    {
        var green = Run("r-1b", "1");
        green.PullRequestsJson = """[{"repo":"api","status":"opened","url":"https://git/pr/1"}]""";
        var boundary = Run("r-3", "3");
        await _h.AddAsync(Run("r-1a", "1", status: RunStatuses.Failed), Step("r-1a"), green, Step("r-1b"),
            Run("r-2", "2", status: "queued"), boundary, Step("r-3"), Spec("r-3", "s-2", "not_started"),
            Run("r-4", "4", status: "cancelled"));
    }

    [Fact]
    public async Task AttemptedAsync_AgreesWithHasAttemptAsync()
    {
        await SeedAsync();
        string[] ids = ["1", "2", "3", "4", "5", ""];

        var attempted = await Attempts.AttemptedAsync("alpha", ids, default);

        foreach (var id in ids)
            attempted.Contains(id).Should().Be(id != "" && await Attempts.HasAttemptAsync("alpha", id, default), id);
        attempted.Should().BeEquivalentTo(["1", "3"], "a queued reservation and a start that never began are no attempts");
    }

    [Fact]
    public async Task LatestEndsAsync_AgreesWithLatestAsyncOnTheEnd()
    {
        await SeedAsync();
        string[] ids = ["1", "2", "3", "4", "5"];

        var ends = await Attempts.LatestEndsAsync("alpha", ids, default);

        foreach (var id in ids)
        {
            var full = await Attempts.LatestAsync("alpha", id, null, default);
            if (full is null) { ends.Should().NotContainKey(id); continue; }
            var end = ends[id];
            (end.RunId, end.Status, end.Finished, end.StoppedAtBoundary, end.OpenedPullRequest)
                .Should().Be((full.RunId, full.Status, full.Finished, full.StoppedAtBoundary, full.OpenedPullRequest), id);
        }
        ends["1"].OpenedPullRequest.Should().BeTrue();
        ends["3"].StoppedAtBoundary.Should().BeTrue();
    }

    [Fact]
    public async Task RequiredOfManyAsync_AnswersEachKeyAsRequiredOfAsync()
    {
        await _h.RequiresAsync("10", "1", "2");
        await _h.RequiresAsync("11", "1");
        var store = _h.Services.GetRequiredService<ITicketRequirementStore>();
        string[] keys = [KeyOf("10"), KeyOf("11"), KeyOf("12")];

        var many = await store.RequiredOfManyAsync(Tracker, keys, default);

        many.Keys.Should().BeEquivalentTo(keys);
        foreach (var key in keys)
            many[key].Should().Equal(await store.RequiredOfAsync(Tracker, key, default), key);
    }
}
