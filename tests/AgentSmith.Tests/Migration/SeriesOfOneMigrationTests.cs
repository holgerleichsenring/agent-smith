using System.Text.Json;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Series;
using AgentSmith.Infrastructure.Migration.Services.SeriesOfOne;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Migration;

/// <summary>2026-11-25-96aea: `database migrate` gives every standalone ticket with live work a series of one — an open pull
/// request, an unfinished or queued run, a queued start — once: a second run, or a ticket a series holds, opens none.
/// 2026-11-26-a12ea: a filed ticket with prerequisites and no run is dormant, not live, and is left alone.</summary>
public sealed class SeriesOfOneMigrationTests : IDisposable
{
    private readonly ServerStateStore _store = new();

    public void Dispose() => _store.Dispose();

    [Fact]
    public async Task RunAsync_OpensOneSeriesOfOnePerTicketWithLiveWork_AndNoneOnASecondRun()
    {
        await using (var db = _store.Context())
        {
            db.Add(Run("r-1", "1", finished: false, "Open run"));      // unfinished code run
            db.Add(Run("r-2", "2", finished: true, "Done run"));       // finished, but its pull request is open
            db.Add(Pr("2"));
            db.Add(Run("r-3", "3", finished: true, "Finished"));       // nothing in flight
            db.Add(Run("r-5", "5", finished: false, "Held"));          // held by an active series already
            db.Add(new SeriesRun { SeriesRunId = "sr-x", SessionId = "c", Project = "p", State = SeriesRunStates.Running,
                ContentJson = JsonSerializer.Serialize(new SeriesRunRecord("sr-x", "c", "p", "op", true, SeriesRunStates.Running,
                    [new SeriesTicket("5", "#5", "Held", false, DateTimeOffset.UnixEpoch)], DateTimeOffset.UnixEpoch)) });
            var queuedRun = Run("r-6", "6", finished: false, "Queued run");
            queuedRun.Status = RunStatuses.Queued;
            db.Add(queuedRun);
            db.Add(Nudge("7", (int)ReworkNudgeOrigin.RequirementsMet, null));                  // a queued start
            db.Add(Nudge("8", (int)ReworkNudgeOrigin.Refresh, $"{(int)ReworkNudgeOrigin.RequirementsMet}:abc")); // claimed when the server stopped
            await db.SaveChangesAsync();
        }
        var migration = new SeriesOfOneMigration(NullLogger<SeriesOfOneMigration>.Instance);

        await using (var db = _store.Context()) (await migration.RunAsync(db, default)).Should().BeTrue();
        await using (var db = _store.Context()) await migration.RunAsync(db, default);

        var opened = await OpenedAsync();
        opened.Select(r => r.Tickets.Single().TicketId).Should().BeEquivalentTo(["1", "2", "6", "7", "8"]);
        opened.Should().OnlyContain(r => r.IsOne && r.MayStartRuns && r.Review == ReviewMode.Each && r.Id == SeriesOfOneMigration.IdOf("p", r.Tickets[0].TicketId));
        opened.Single(r => r.Tickets[0].TicketId == "1").Tickets[0].Title.Should().Be("Open run");
        opened.Where(r => r.Wakes.Count > 0).Select(r => r.Tickets[0].TicketId).Should().BeEquivalentTo(["7", "8"],
            "only a queued start is its master's to start");
        opened.Single(r => r.Tickets[0].TicketId == "7").Wakes.Should().ContainSingle(w => w.Kind == SeriesWakeKinds.Prerequisite);
    }

    // 2026-11-26-a12ea: in production the migration adopted tickets filed weeks earlier with prerequisites and no run, and their
    // masters started them; the old server only ever started such a ticket at the moment its last prerequisite landed.
    [Fact]
    public async Task RunAsync_LeavesADormantFiledTicket_AQueuedStartOnAWorkedTicket_AndACancelledRunAlone()
    {
        await using (var db = _store.Context())
        {
            db.Add(new TicketRequirement { Tracker = "gh", TicketKey = "github-4", TicketId = "4", Project = "p",
                RequiredTicketKey = "github-1", RequiredTicketId = "1", StartsWhenMet = true, FiledInConversation = "c" });
            db.Add(Run("r-9", "9", finished: true, "Worked"));
            db.Add(Nudge("9", (int)ReworkNudgeOrigin.RequirementsMet, null));                  // does nothing on a worked ticket
            var cancelled = Run("r-10", "10", finished: false, "Cancelled");
            cancelled.CancelRequested = true;
            db.Add(cancelled);
            var parked = Run("r-11", "11", finished: false, "Cancelled while parked");
            (parked.Status, parked.CancelRequested) = (RunStatuses.WaitingForInput, true);
            db.Add(parked);
            await db.SaveChangesAsync();
        }

        await using (var db = _store.Context()) await new SeriesOfOneMigration(NullLogger<SeriesOfOneMigration>.Instance).RunAsync(db, default);

        (await OpenedAsync()).Should().BeEmpty("none of them has live work");
    }

    private async Task<List<SeriesRunRecord>> OpenedAsync()
    {
        await using var db = _store.Context();
        return [.. (await db.Set<SeriesRun>().AsNoTracking().Where(s => s.SeriesRunId != "sr-x").ToListAsync())
            .Select(s => JsonSerializer.Deserialize<SeriesRunRecord>(s.ContentJson)!)];
    }

    private static Run Run(string id, string ticket, bool finished, string title) => new()
    {
        Id = id, Project = "p", Pipeline = "code", TicketId = ticket, TicketTitle = title,
        Status = finished ? RunStatuses.Success : RunStatuses.Running, StartedAt = DateTimeOffset.UnixEpoch,
        FinishedAt = finished ? DateTimeOffset.UnixEpoch : null,
    };

    private static PrReviewRecord Pr(string ticket) => new()
    {
        PrKey = $"k{ticket}", Url = $"https://host/pr/{ticket}", Repo = "api", Number = ticket, Project = "p", TicketId = ticket,
        RunId = "r-2", State = PrReviewStates.Open,
    };

    private static ReworkNudge Nudge(string ticket, int origin, string? claim) => new()
    {
        Project = "p", TicketId = ticket, Origin = origin, ClaimToken = claim, DueTicks = DateTimeOffset.UnixEpoch.UtcTicks,
    };
}
