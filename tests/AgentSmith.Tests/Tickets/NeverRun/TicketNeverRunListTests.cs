using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Tickets;
using AgentSmith.Server.Extensions;
using AgentSmith.Server.Models;
using FluentAssertions;
using Microsoft.AspNetCore.Http.HttpResults;
using static AgentSmith.Tests.Tickets.NeverRun.NeverRunFixtures;
using static AgentSmith.Tests.Tickets.TicketStoreHarness;

namespace AgentSmith.Tests.Tickets.NeverRun;

/// <summary>
/// 2026-10-14-dbcen: a ticket filed here with no code run on record is a list row — blocked while a ticket it
/// requires has not landed, filed otherwise, done once it landed — read from its filing record, never from the
/// tracker; it leads the first page only and the cursor pages the run rows alone.
/// </summary>
public sealed class TicketNeverRunListTests : IDisposable
{
    private readonly TicketStoreHarness _h = new();

    public void Dispose() => _h.Dispose();

    [Fact]
    public async Task TicketList_FiledTicketWithNoRun_IsListedAsFiled()
    {
        await _h.AddAsync(Filing("59", "Orders: list screen"));

        var row = (await _h.ListAsync()).Tickets.Single();

        (row.Bucket, row.TicketId, row.AttemptCount, row.Cost, row.NewestRunId).Should().Be((TicketBucket.Filed, "59", 0, 0m, ""));
        row.WaitsFor.Should().BeEmpty();
        row.Since.Should().Be(T);
    }

    [Fact]
    public async Task TicketList_FiledTicketWithUnlandedRequirement_IsBlockedAndNamesWhatItWaitsForByTrackerKey()
    {
        await _h.AddAsync(Filing("59", "Orders: list screen"), Filing("58", "Orders: API"));
        await _h.RequiresAsync("59", "58", "41");

        var row = (await _h.ListAsync()).Tickets.Single(t => t.TicketId == "59");

        row.Bucket.Should().Be(TicketBucket.Blocked);
        row.WaitsFor!.Select(w => (w.TicketId, w.Key, w.Title)).Should().Equal(("58", "#58", "Orders: API"), ("41", "#41", "#41"));
    }

    [Fact]
    public async Task TicketList_FiledTicketWhoseRunsWereDeleted_IsListedAsFiled()
    {
        await _h.AddAsync(Filing("42"), Run("r-1"), Step("r-1"));
        (await _h.ListAsync()).Tickets.Single().NewestRunId.Should().Be("r-1");

        await _h.DeleteRunAsync("r-1");

        (await _h.ListAsync()).Tickets.Single().Bucket.Should().Be(TicketBucket.Filed);
    }

    [Fact]
    public async Task TicketList_NeverRunTicketLandedByHand_IsDoneOnlyUnderTheDoneFilterAndDatedByItsLanding()
    {
        await _h.AddAsync(Filing("59"));
        await _h.LandAsync("59", at: T.AddHours(2));

        var all = await _h.ListAsync();
        var done = await _h.ListAsync(TicketBucket.Done);

        all.Tickets.Should().BeEmpty("a never-run done ticket would otherwise lead every first page");
        all.Counts.Done.Should().Be(1);
        done.Tickets.Single().Should().Match<TicketSummaryView>(t => t.Bucket == TicketBucket.Done && t.Since == T.AddHours(2));
    }

    [Fact]
    public async Task TicketList_FilingBeforeProjectColumn_IsNotListed()
    {
        var older = Filing("59");
        older.Project = "";
        await _h.AddAsync(older);

        (await _h.ListAsync(null)).Tickets.Should().BeEmpty();
        (await _h.List.ReadAsync(new TicketListRequest(null, null, null, null), default)).Tickets.Should().BeEmpty();
    }

    [Fact]
    public async Task TicketList_BlockedAndFiledRows_LeadTheFirstPageAndTakeNoCursor()
    {
        await _h.AddAsync(Run("r-1", "42"), Step("r-1"), Run("r-2", "43"), Step("r-2"), Filing("59"), Filing("60"));
        await _h.RequiresAsync("60", "59");

        var first = await _h.ListAsync(limit: 1);
        var second = await _h.ListAsync(cursor: first.NextCursor, limit: 1);

        first.Tickets.Select(t => (t.TicketId, t.Bucket)).Should().Equal(
            ("59", TicketBucket.Filed), ("60", TicketBucket.Blocked), ("43", TicketBucket.Idle));
        first.NextCursor.Should().Be("r-2");
        second.Tickets.Select(t => t.TicketId).Should().Equal("42");
    }

    [Fact]
    public async Task TicketList_NextCursor_ComputedOverRunRowsOnly()
    {
        await _h.AddAsync(Run("r-1", "42"), Step("r-1"), Run("r-2", "43"), Step("r-2"), Filing("59"));

        var page = await _h.ListAsync(limit: 2);

        page.Tickets.Should().HaveCount(3, "the never-run row is added on top of a full run page");
        page.NextCursor.Should().BeNull("two run rows fill the page and none is left");
        (await _h.ListAsync(limit: 1)).NextCursor.Should().Be("r-2");
    }

    [Fact]
    public async Task TicketList_TicketFiledUnderOneProjectRunUnderAnother_IsListedOnce()
    {
        _h.Config.Projects["beta"] = new ResolvedProject
        {
            Name = "beta", Tracker = new TrackerConnection { Name = Tracker, Type = TrackerType.GitHub },
        };
        await _h.AddAsync(Filing("59", project: "alpha"), Run("r-1", "59", project: "beta"), Step("r-1"));

        var all = await _h.List.ReadAsync(new TicketListRequest(null, null, null, null), default);

        all.Tickets.Should().ContainSingle().Which.Project.Should().Be("beta", "the run-bearing row is the ticket");
        (await _h.ListAsync()).Tickets.Should().BeEmpty("alpha's filing ran under beta");
    }

    [Fact]
    public async Task TicketList_Counts_IncludeBlockedAndFiled()
    {
        await _h.AddAsync(Filing("59"), Filing("60"), Filing("61"));
        await _h.RequiresAsync("60", "59");

        var counts = (await _h.ListAsync()).Counts;

        (counts.Blocked, counts.Filed).Should().Be((1, 2));
    }

    [Theory]
    [InlineData(TicketBucket.Blocked)]
    [InlineData(TicketBucket.Filed)]
    public async Task TicketQueryEndpoints_BucketBlockedAndFiled_AreAccepted(string bucket)
    {
        await _h.AddAsync(Filing("59"), Filing("60"));
        await _h.RequiresAsync("60", "59");

        var result = await TicketQueryEndpoints.ListAsync("alpha", bucket, null, null, _h.List, default);

        result.Should().BeOfType<Ok<TicketListView>>().Which.Value!.Tickets.Should().ContainSingle(t => t.Bucket == bucket);
    }

    [Fact]
    public async Task TicketList_FiledSpecTicketWithNoRun_IsFiledWithoutTheDerivationLine()
    {
        await _h.AddAsync(Filing("59", "Port the export", ticketText: false));

        var row = (await _h.ListAsync()).Tickets.Single();

        (row.Bucket, row.TicketText).Should().Be((TicketBucket.Filed, (bool?)false));
    }

    [Fact]
    public async Task TicketList_NeverRunRow_CarriesTicketKeyPlatformAndTitleFromTheStoredRecord()
    {
        await _h.AddAsync(Filing("59", "Orders: list screen"), Filing("60"));

        var rows = (await _h.ListAsync()).Tickets;

        rows.Single(t => t.TicketId == "59").Should().Match<TicketSummaryView>(t =>
            t.TicketKey == "github-59" && t.Platform == "github" && t.Title == "Orders: list screen" && t.TicketText == true);
        rows.Single(t => t.TicketId == "60").Title.Should().Be("#60", "a record without a goal is named by its tracker key");
    }

    [Fact]
    public async Task TicketList_RunBearingRow_CarriesNoWaitsForAndNoTicketText()
    {
        await _h.AddAsync(Run("r-1"), Step("r-1"));

        var row = (await _h.ListAsync()).Tickets.Single();

        (row.WaitsFor, row.TicketText).Should().Be((null, null));
    }

    [Fact]
    public async Task TicketDetail_NeverRunFiledTicket_ReturnsADetailWithNoAttempts()
    {
        await _h.AddAsync(Filing("59", "Orders: list screen"));
        await _h.RequiresAsync("59", "58");

        var detail = await _h.Detail.ReadAsync("alpha", "59", default);

        detail!.Summary.Should().Match<TicketSummaryView>(s => s.Bucket == TicketBucket.Blocked && s.TicketKey == "github-59");
        (detail.Attempts.Count, detail.OtherRuns.Count).Should().Be((0, 0));
    }

    [Fact]
    public async Task TicketDetail_NoRunAndNoFiling_IsNotFound()
    {
        await _h.RequiresAsync("59", "58");

        (await TicketQueryEndpoints.DetailAsync("alpha", "58", _h.Detail, default)).Should().BeOfType<NotFound>();
    }
}
