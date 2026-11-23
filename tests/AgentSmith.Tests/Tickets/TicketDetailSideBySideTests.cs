using System.Text.Json;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Runs;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Server.Models;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using static AgentSmith.Tests.Tickets.TicketConversationFixtures;
using static AgentSmith.Tests.Tickets.TicketStoreHarness;

namespace AgentSmith.Tests.Tickets;

/// <summary>
/// 2026-11-23-1e76a: the detail read's reads after the run query run side by side, each in its own scope — over a store
/// with a connection per context, as in production, every command held until the read has sent all it can. The view
/// does not depend on which read lands first.
/// </summary>
public sealed class TicketDetailSideBySideTests : IDisposable
{
    private readonly TicketStoreHarness _h = new(connectionPerContext: true);

    public void Dispose() => _h.Dispose();

    private async Task TicketAsync()
    {
        var failed = Run("r-1", status: "failed");
        failed.ActsReadAt = T;
        var parked = Run("r-2", status: "waiting_for_input");
        parked.PullRequestsJson = RunStoryJson.Serialize(new List<RunPullRequestView>
            { new("api", PullRequestStatuses.Opened, PrUrl("7"), null, T) });
        await _h.AddAsync(failed, Step("r-1"), new RunAttempt { RunId = "r-1", TicketFingerprint = "f" }, Spec("r-1", "s1", "failed"),
            parked, Step("r-2"), Spec("r-2", "s1", "in_progress"), Checkpoint("r-2"), Record("7", PrReviewStates.Open),
            Run("r-3", pipeline: "spec-derive"), Run("r-4", pipeline: "pr-review"));
    }

    // Wait until the read has sent every command it can without an answer, then answer one; again until it answers.
    private async Task<TicketDetailView?> HeldReadAsync(bool newestFirst)
    {
        _h.Held.Holding = true;
        var read = _h.Detail.ReadAsync("alpha", "42", default);
        while (!read.IsCompleted)
        {
            await TestWaits.UntilAsync(() => _h.Held.Held > 0 || read.IsCompleted, "a command or the answer");
            var held = _h.Held.Held;
            if (await TestWaits.StaysAsync(() => _h.Held.Held == held)) _h.Held.ReleaseOne(newestFirst);
        }
        _h.Held.Holding = false;
        return await read;
    }

    [Fact]
    public async Task TicketDetail_EveryCommandHeld_HoldsMoreThanOneAtOnce()
    {
        await TicketAsync();
        await _h.Detail.ReadAsync("alpha", "42", default); // every query compiled, so a wave is not split by compiling

        await HeldReadAsync(newestFirst: true);

        _h.Held.MostHeldAtOnce.Should().BeGreaterThan(1, "the reads after the run query do not wait on each other");
    }

    [Fact]
    public async Task TicketDetail_ReadsLandingInAnyOrder_SameView()
    {
        await TicketAsync();
        var plain = JsonSerializer.Serialize(await _h.Detail.ReadAsync("alpha", "42", default));

        var newestFirst = JsonSerializer.Serialize(await HeldReadAsync(newestFirst: true));
        var oldestFirst = JsonSerializer.Serialize(await HeldReadAsync(newestFirst: false));

        newestFirst.Should().Be(plain);
        oldestFirst.Should().Be(plain);
    }
}
