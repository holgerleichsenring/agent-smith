using AgentSmith.Application.Services.Persistence;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Specs;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace AgentSmith.Tests.SpecDialog;

/// <summary>
/// 2026-09-25-8e51d: a conversation BOUND to a ticket shows the specification a person approved
/// for that ticket, even when the conversation filed nothing itself.
/// <para>
/// The ticket is read off the SESSION ROW and never off a request — the reads on this surface are
/// addressed by dialog id precisely so nobody can follow a ticket by asking for it — and the
/// record is keyed by the conversation's own tracker connection, because the spec key carries
/// only the tracker TYPE.
/// </para>
/// </summary>
public sealed class ApprovedSetInThePaneTests : IDisposable
{
    private const string Dialog = "d-1";
    private const string Tracker = "sample-tracker";
    private readonly SqliteConnection _connection;
    private readonly AgentSmithDbContext _context;
    private readonly InMemorySpecApprovalStore _approvals = new();

    public ApprovedSetInThePaneTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _context = new AgentSmithDbContext(
            new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options);
        MigratedStoreTemplate.CopyInto(_context);
    }

    [Fact]
    public async Task TicketSpecs_ABoundTicketWithAnApprovedSet_ShowsItsSpecsAndItsApproval()
    {
        await BindAsync("jira-1");
        await FileAsync(ApprovedSets.Record("jira-1", ApprovedSets.Noon, ["p0001a"], tracker: Tracker));

        var view = await Sut().ForAsync(Dialog, default);

        view.Should().NotBeNull();
        view!.Specs.Should().ContainSingle().Which.SpecId.Should().Be("p0001a");
        view.BranchUnread.Should().BeNull();
        view.ApprovedAt.Should().Be(ApprovedSets.Noon,
            "the pane says whose approval it shows and when — the branch is what a run reads, and "
            + "for a worked ticket the two can differ");
    }

    // 2026-11-17-a0b6c: the specs are the branch's — a record whose text differs shows the branch's goal.
    [Fact]
    public async Task TicketSpecs_TheRecordDiffersFromTheBranch_ShowsTheBranch()
    {
        await BindAsync("jira-1");
        await FileAsync(ApprovedSets.Record("jira-1", ApprovedSets.Noon, ["p0001a"], tracker: Tracker));
        await _approvals.SaveAsync(ApprovedSets.Record("jira-1", ApprovedSets.Noon, tracker: Tracker) with
        {
            Set = ApprovedSets.Set("jira-1", [ApprovedSets.Spec("p0001a", "A goal only the record holds")], ApprovedSets.Approval(ApprovedSets.Noon)),
        }, default);

        var view = await Sut().ForAsync(Dialog, default);

        view!.Specs.Single().Goal.Should().Be("Do the thing", "the branch is the spec's text");
    }

    // 2026-11-17-a0b6c: a branch the host cannot read is named, and no copy stands in for it.
    [Fact]
    public async Task TicketSpecs_TheBranchCannotBeRead_IsNamedAndShowsNoSpecs()
    {
        await BindAsync("jira-1");
        await FileAsync(ApprovedSets.Record("jira-1", ApprovedSets.Noon, ["p0001a"], tracker: Tracker));
        _sources.ReadThrows = new HttpRequestException("503 from the host");

        var view = await Sut().ForAsync(Dialog, default);

        view!.Specs.Should().BeEmpty();
        view.BranchUnread.Should().Contain("503 from the host");
    }

    [Fact]
    public async Task TicketSpecs_ABoundTicketWithNoSet_ShowsNothingRatherThanFailing()
    {
        await BindAsync("jira-2");

        (await Sut().ForAsync(Dialog, default)).Should().BeNull(
            "a hand-written ticket has no approved record, and that is the ordinary case");
    }

    [Fact]
    public async Task TicketSpecs_AConversationWithNoTicket_ShowsNothing()
    {
        await BindAsync(ticketKey: null);
        await _approvals.SaveAsync(
            ApprovedSets.Record("jira-1", ApprovedSets.Noon, tracker: Tracker), default);

        (await Sut().ForAsync(Dialog, default)).Should().BeNull();
    }

    private readonly RecordingBranchSources _sources = new();
    private readonly AgentSmith.Application.Services.Persistence.InMemorySpecSetPointerStore _pointers = new();

    private static readonly ResolvedProject Sample = new()
    {
        Name = "sample", Tracker = new TrackerConnection { Name = Tracker, Type = TrackerType.Jira },
        Repos = [new RepoConnection { Name = "sample-api" }],
    };

    private ApprovedSetForConversation Sut() => new(
        new SpecDialogSessionRepository(_context), _approvals,
        SeriesDoubles.BranchSpecs(new AgentSmithConfig { Projects = new() { ["sample"] = Sample } }, _sources, _pointers));

    // The record and its branch write, as filing leaves them.
    private async Task FileAsync(SpecApprovalRecord record)
    {
        record = record with
        {
            CarryingRepo = "sample-api", Repositories = ["sample-api"], TicketId = "1",
            Set = record.Set with { Series = "2026-09-17-0a0a" },
        };
        await _approvals.SaveAsync(record, default);
        var ticket = new AgentSmith.Domain.Entities.Ticket(new AgentSmith.Domain.Models.TicketId("1"), "t", "b", null, "open", "jira", []);
        var written = await ApprovedSetDoubles.Branch(_sources, _pointers)
            .WriteAsync(Sample, record, new AgentSmith.Domain.Models.TicketId("1"), ticket, default);
        written.Written.Should().BeTrue(written.Error);
    }

    private Task BindAsync(string? ticketKey) =>
        new SpecDialogSessionRepository(_context).AddAsync(
            new SpecDialogSession
            {
                SessionId = "s-1",
                Platform = "dashboard",
                ChannelId = Dialog,
                ThreadId = Dialog,
                UserId = "someone",
                Project = "sample",
                Tracker = ticketKey is null ? null : Tracker,
                TicketKey = ticketKey,
                LastActivityAt = DateTimeOffset.UtcNow,
            },
            CancellationToken.None);

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}
