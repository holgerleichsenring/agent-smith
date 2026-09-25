using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Tests.Persistence;

/// <summary>
/// 2026-09-25-c1f7: the relational half of the discovery listing, over the SHIPPED migrations —
/// the column and the index are part of what is asserted, because the in-memory store cannot
/// prove a migration exists.
/// </summary>
public sealed class ApprovedSpecSetOutstandingTests : IDisposable
{
    private const string Tracker = ApprovedSets.Tracker;

    private readonly SqliteConnection _connection = MigratedStoreTemplate.OpenCopy();

    private readonly AgentSmithDbContext _context;

    private readonly ApprovedSpecSetRepository _repository;

    public ApprovedSpecSetOutstandingTests()
    {
        _context = MigratedStoreTemplate.Context(_connection);
        _repository = new ApprovedSpecSetRepository(_context);
    }

    /// <summary>
    /// Oldest first — by the ROW ID, which is the order the records were written. SQLite cannot
    /// ORDER BY a DateTimeOffset, and ordering after the take would bound the wrong set.
    /// </summary>
    [Fact]
    public async Task ListOutstanding_OldestFirst_AndReportsWhatTheLimitLeftOut()
    {
        await SaveAsync("jira-a", "A-1", ApprovedSets.Noon);
        await SaveAsync("jira-b", "B-2", ApprovedSets.Noon.AddMinutes(1));
        await SaveAsync("jira-c", "C-3", ApprovedSets.Noon.AddMinutes(2));

        var outstanding = await _repository.ListOutstandingAsync(Tracker, 2, CancellationToken.None);

        outstanding.TicketIds.Should().Equal("A-1", "B-2");
        outstanding.Omitted.Should().Be(1);
    }

    [Fact]
    public async Task ListOutstanding_ARecordWithoutATicketId_IsSkipped()
    {
        await SaveAsync("jira-a", ticketId: string.Empty, ApprovedSets.Noon);

        (await _repository.ListOutstandingAsync(Tracker, 10, CancellationToken.None))
            .TicketIds.Should().BeEmpty("the tracker's own id cannot be recovered from the key");
    }

    [Fact]
    public async Task ListOutstanding_AnotherTrackersRecord_IsNotNamed()
    {
        await SaveAsync("jira-a", "A-1", ApprovedSets.Noon, tracker: "another-jira");

        (await _repository.ListOutstandingAsync(Tracker, 10, CancellationToken.None))
            .TicketIds.Should().BeEmpty();
    }

    [Fact]
    public async Task MarkSatisfied_ARunFinishedTheTicket_TheRecordStopsBeingOutstanding()
    {
        await SaveAsync("jira-a", "A-1", ApprovedSets.Noon);

        await _repository.MarkSatisfiedAsync(
            Tracker, "jira-a", ApprovedSets.Noon.AddHours(1), CancellationToken.None);

        (await _repository.ListOutstandingAsync(Tracker, 10, CancellationToken.None))
            .TicketIds.Should().BeEmpty();
        (await RowAsync("jira-a")).SatisfiedAt.Should().Be(ApprovedSets.Noon.AddHours(1));
    }

    [Fact]
    public async Task MarkSatisfied_NoSuchRecord_IsSilent()
    {
        var act = () => _repository.MarkSatisfiedAsync(
            Tracker, "jira-nobody-approved", ApprovedSets.Noon, CancellationToken.None);

        await act.Should().NotThrowAsync("a run finalizing an unapproved ticket is the ordinary case");
    }

    /// <summary>
    /// A SECOND APPROVAL IS NEW WORK. Re-approving a ticket a run already finished has to make the
    /// record outstanding again, or the amendment would never be discovered.
    /// </summary>
    [Fact]
    public async Task Save_OverASatisfiedRecord_MakesItOutstandingAgain()
    {
        await SaveAsync("jira-a", "A-1", ApprovedSets.Noon);
        await _repository.MarkSatisfiedAsync(
            Tracker, "jira-a", ApprovedSets.Noon.AddHours(1), CancellationToken.None);

        await SaveAsync("jira-a", "A-1", ApprovedSets.Noon.AddHours(2));

        (await _repository.ListOutstandingAsync(Tracker, 10, CancellationToken.None))
            .TicketIds.Should().Equal("A-1");
    }

    [Fact]
    public async Task Get_ARecordWrittenWithATicketId_ReadsItBack()
    {
        await SaveAsync("jira-a", "DPG-1239", ApprovedSets.Noon);

        var record = await _repository.GetAsync(Tracker, "jira-a", CancellationToken.None);

        record!.TicketId.Should().Be("DPG-1239", "the id is the tracker's own, not the key's");
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private Task SaveAsync(
        string key, string ticketId, DateTimeOffset at, string tracker = Tracker) =>
        _repository.SaveAsync(
            ApprovedSets.Record(key, at, tracker: tracker, ticketId: ticketId),
            CancellationToken.None);

    private async Task<ApprovedSpecSet> RowAsync(string key)
    {
        _context.ChangeTracker.Clear();
        return await _context.Set<ApprovedSpecSet>().AsNoTracking()
            .SingleAsync(a => a.SpecKey == key);
    }
}
