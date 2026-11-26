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

/// <summary>2026-11-25-96aea: `database migrate` gives every standalone ticket in flight a series of one — an open pull
/// request, an unfinished run, a start waiting on prerequisites — once: a second run, or a ticket a series holds, opens none.</summary>
public sealed class SeriesOfOneMigrationTests : IDisposable
{
    private readonly ServerStateStore _store = new();

    public void Dispose() => _store.Dispose();

    [Fact]
    public async Task RunAsync_OpensOneSeriesOfOnePerTicketInFlight_AndNoneOnASecondRun()
    {
        await using (var db = _store.Context())
        {
            db.Add(Run("r-1", "1", finished: false, "Open run"));      // unfinished code run
            db.Add(Run("r-2", "2", finished: true, "Done run"));       // finished, but its pull request is open
            db.Add(Pr("2"));
            db.Add(Run("r-3", "3", finished: true, "Finished"));       // nothing in flight
            db.Add(new TicketRequirement { Tracker = "gh", TicketKey = "github-4", TicketId = "4", Project = "p",
                RequiredTicketKey = "github-1", RequiredTicketId = "1", StartsWhenMet = true, FiledInConversation = "c" });
            db.Add(Run("r-5", "5", finished: false, "Held"));          // held by an active series already
            db.Add(new SeriesRun { SeriesRunId = "sr-x", SessionId = "c", Project = "p", State = SeriesRunStates.Running,
                ContentJson = JsonSerializer.Serialize(new SeriesRunRecord("sr-x", "c", "p", "op", true, SeriesRunStates.Running,
                    [new SeriesTicket("5", "#5", "Held", false, DateTimeOffset.UnixEpoch)], DateTimeOffset.UnixEpoch)) });
            await db.SaveChangesAsync();
        }
        var migration = new SeriesOfOneMigration(NullLogger<SeriesOfOneMigration>.Instance);

        await using (var db = _store.Context()) (await migration.RunAsync(db, default)).Should().BeTrue();
        await using (var db = _store.Context()) await migration.RunAsync(db, default);

        var opened = await OpenedAsync();
        opened.Select(r => r.Tickets.Single().TicketId).Should().BeEquivalentTo(["1", "2", "4"]);
        opened.Should().OnlyContain(r => r.IsOne && r.MayStartRuns && r.Review == ReviewMode.Each && r.Id == SeriesOfOneMigration.IdOf("p", r.Tickets[0].TicketId));
        opened.Single(r => r.Tickets[0].TicketId == "1").Tickets[0].Title.Should().Be("Open run");
        opened.Single(r => r.Tickets[0].TicketId == "4").Wakes.Should().ContainSingle(w => w.Kind == SeriesWakeKinds.Prerequisite,
            "a ticket filed waiting on its prerequisites is its master's to start");
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
}
