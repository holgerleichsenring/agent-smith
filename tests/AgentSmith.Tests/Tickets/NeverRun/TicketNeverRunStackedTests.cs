using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Server.Models;
using FluentAssertions;
using static AgentSmith.Tests.Tickets.NeverRun.NeverRunFixtures;
using static AgentSmith.Tests.Tickets.TicketStoreHarness;

namespace AgentSmith.Tests.Tickets.NeverRun;

/// <summary>
/// 2026-11-17-b34ec: a never-run dependent's marker reads the rule a start reads — under on_green_push a prerequisite
/// that pushed a green branch with a pull request lifts the wait, under on_merge only a landing does.
/// </summary>
public sealed class TicketNeverRunStackedTests : IDisposable
{
    private readonly TicketStoreHarness _h = new();

    public void Dispose() => _h.Dispose();

    private async Task GreenPrerequisiteAsync()
    {
        var green = Run("r-58", "58");
        green.PullRequestsJson = """[{"repo":"api","status":"opened","url":"https://git/pr/58"}]""";
        await _h.AddAsync(Filing("59", "Orders: list screen"), Filing("58", "Orders: API"), green, Step("r-58"));
        await _h.RequiresAsync("59", "58");
    }

    [Fact]
    public async Task OnGreenPush_ADependentOfAGreenPrerequisite_ReadsReady()
    {
        await GreenPrerequisiteAsync();

        var row = (await _h.ListAsync()).Tickets.Single(t => t.TicketId == "59");

        row.Bucket.Should().Be(TicketBucket.Filed, "the gate admits it, stacked on 58's branch");
        row.WaitsFor.Should().BeEmpty();
    }

    [Fact]
    public async Task OnMerge_TheSameDependent_StaysBlocked()
    {
        _h.Config.Projects["alpha"] = _h.Config.Projects["alpha"] with { RequiresStart = RequiresStart.OnMerge };
        await GreenPrerequisiteAsync();

        var row = (await _h.ListAsync()).Tickets.Single(t => t.TicketId == "59");

        row.Bucket.Should().Be(TicketBucket.Blocked);
        row.WaitsFor!.Select(w => w.TicketId).Should().Equal("58");
    }
}
