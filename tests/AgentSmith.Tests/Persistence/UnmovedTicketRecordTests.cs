using AgentSmith.Contracts.Models;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Core.Services.Configuration.Studio;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Tests.Persistence;

/// <summary>
/// 2026-09-18-c1a7: the record of a ticket a run could not move, against the REAL schema. It is
/// keyed by project and ticket — the shape the claim path already reads — and it stands only
/// for as long as the configuration it was written under.
/// </summary>
public sealed class UnmovedTicketRecordTests : IDisposable
{
    private readonly SqliteConnection _connection = MigratedStoreTemplate.OpenCopy();

    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task Record_RunEndedWithItsTicketUnmoved_IsReadableByProjectAndTicket()
    {
        await RecordAsync(new UnmovedTicketFact(
            "p1", "42", "tracker-a", "Failed", TicketFinalizeOutcome.TrackerRejectedTheStatus));

        var standing = await Repository().FindStandingAsync("p1", "42", "tracker-a", CancellationToken.None);

        standing.Should().NotBeNull();
        standing!.ConfiguredStatus.Should().Be("Failed");
        standing.Outcome.Should().Be(TicketFinalizeOutcome.TrackerRejectedTheStatus);
        (await Repository().FindStandingAsync("p1", "43", "tracker-a", CancellationToken.None))
            .Should().BeNull("the record is about one ticket, not the project");
    }

    [Fact]
    public async Task Record_SecondFailureOfTheSameTicket_OverwritesRatherThanStacks()
    {
        await RecordAsync(new UnmovedTicketFact("p1", "42", "tracker-a", "Failed", TicketFinalizeOutcome.Moved));
        await RecordAsync(new UnmovedTicketFact(
            "p1", "42", "tracker-a", "Rejected", TicketFinalizeOutcome.NoTransitionToTheStatus));

        using var context = Context();
        context.UnmovedTickets.Should().ContainSingle()
            .Which.ConfiguredStatus.Should().Be("Rejected");
    }

    [Fact]
    public async Task Record_TrackerDocumentChanged_NoLongerStands()
    {
        await SaveConfigDocumentAsync("tracker", "tracker-a", version: 3);
        await RecordAsync(new UnmovedTicketFact(
            "p1", "42", "tracker-a", "Failed", TicketFinalizeOutcome.TrackerRejectedTheStatus));

        await SaveConfigDocumentAsync("tracker", "tracker-a", version: 4);

        (await Repository().FindStandingAsync("p1", "42", "tracker-a", CancellationToken.None))
            .Should().BeNull("the operator's fix bumps the document the status came from");
    }

    // A record the configuration has moved past is DROPPED, not merely ignored: left lying, it
    // would stand again the day an export/re-import rewound the version it names.
    [Fact]
    public async Task Record_TrackerDocumentChanged_IsDroppedAndCannotComeBack()
    {
        await SaveConfigDocumentAsync("tracker", "tracker-a", version: 3);
        await RecordAsync(new UnmovedTicketFact(
            "p1", "42", "tracker-a", "Failed", TicketFinalizeOutcome.TrackerRejectedTheStatus));
        await SaveConfigDocumentAsync("tracker", "tracker-a", version: 4);

        await Repository().FindStandingAsync("p1", "42", "tracker-a", CancellationToken.None);

        using var context = Context();
        context.UnmovedTickets.Should().BeEmpty();
    }

    // Moved is the zero value of the persisted column as well as of the enum, so a defaulted row
    // must not read as a refusal of an empty status.
    [Fact]
    public async Task Record_ARowThatSaysTheTicketMoved_DoesNotStand()
    {
        using (var seed = Context())
        {
            seed.UnmovedTickets.Add(new UnmovedTicket
            {
                Project = "p1", TicketId = "42", Tracker = "tracker-a",
                ConfiguredStatus = string.Empty, Outcome = (int)TicketFinalizeOutcome.Moved,
            });
            await seed.SaveChangesAsync();
        }

        (await Repository().FindStandingAsync("p1", "42", "tracker-a", CancellationToken.None))
            .Should().BeNull();
    }

    // The two document types are the config taxonomy's own, spelled here because the persistence
    // assembly cannot reference it. A rename would make every version read zero — and the stop
    // permanent — with nothing else to notice.
    [Fact]
    public void Record_TheDocumentTypesItReads_AreTheConfigTaxonomys()
    {
        UnmovedTicketRepository.TrackerDocType.Should().Be(ConfigDocTypes.Tracker);
        UnmovedTicketRepository.ProjectDocType.Should().Be(ConfigDocTypes.Project);
    }

    [Fact]
    public async Task Record_Cleared_NoLongerStands()
    {
        await RecordAsync(new UnmovedTicketFact(
            "p1", "42", "tracker-a", "Failed", TicketFinalizeOutcome.TrackerRejectedTheStatus));

        await Repository().ClearAsync("p1", "42", CancellationToken.None);

        (await Repository().FindStandingAsync("p1", "42", "tracker-a", CancellationToken.None)).Should().BeNull();
    }

    private Task RecordAsync(UnmovedTicketFact fact) => Repository().RecordAsync(fact, CancellationToken.None);

    private async Task SaveConfigDocumentAsync(string type, string id, int version)
    {
        using var context = Context();
        var row = await context.ConfigEntities
            .FirstOrDefaultAsync(c => c.EntityType == type && c.EntityId == id);
        if (row is null)
        {
            row = new ConfigEntity { EntityType = type, EntityId = id, Doc = "{}", UpdatedBy = "operator" };
            context.ConfigEntities.Add(row);
        }
        row.Version = version;
        await context.SaveChangesAsync();
    }

    private UnmovedTicketRepository Repository() => new(Context());

    private AgentSmithDbContext Context() =>
        new(new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options);
}
