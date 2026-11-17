using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Tests.Persistence;

/// <summary>
/// 2026-11-17-a0b6d: a conversation's proposals are rows of one table, and every transition is a compare-and-set on a
/// row's state. Two contexts over one store stand in for two replicas.
/// </summary>
public sealed class SpecDialogProposalTableTests : IDisposable
{
    private const string Platform = "slack";
    private const string Thread = "th-a0b6d";
    private const string Session = "s-a0b6d";

    private readonly SqliteConnection _connection = MigratedStoreTemplate.OpenCopy();
    private readonly AgentSmithDbContext _context;

    public SpecDialogProposalTableTests()
    {
        _context = MigratedStoreTemplate.Context(_connection);
        _context.Add(new SpecDialogSession
        {
            SessionId = Session, Platform = Platform, ChannelId = "C1", ThreadId = Thread, UserId = "U1", Project = "p", IsOpen = true,
        });
        _context.SaveChanges();
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task SetAwaiting_OverAnAwaitingProposal_SupersedesIt()
    {
        var (first, _) = await Claim().SetAwaitingAsync(Platform, Thread, "{\"n\":1}", "run-1", default);

        var (second, replaced) = await Claim().SetAwaitingAsync(Platform, Thread, "{\"n\":2}", "run-2", default);

        replaced.Should().Be(first);
        (await StateOf(first), await StateOf(second)).Should().Be((SpecDialogProposalClaim.Superseded, SpecDialogProposalClaim.Awaiting));
        (await Claim().ReadAsync(Platform, Thread, default)).AwaitingId.Should().Be(second);
    }

    [Fact]
    public async Task ClaimThenRelease_AwaitsAgainUnderItsId()
    {
        var (id, _) = await Claim().SetAwaitingAsync(Platform, Thread, "{}", default);

        (await Claim().ClaimAsync(Platform, Thread, id, default)).Should().Be("{}");
        (await StateOf(id)).Should().Be(SpecDialogProposalClaim.Filing);
        (await Slot().ReleaseAsync(Platform, Thread, id, default)).Should().BeTrue();

        (await StateOf(id)).Should().Be(SpecDialogProposalClaim.Awaiting);
    }

    [Fact]
    public async Task ClaimThenClear_IsFiled()
    {
        var (id, _) = await Claim().SetAwaitingAsync(Platform, Thread, "{}", default);
        await Claim().ClaimAsync(Platform, Thread, id, default);

        (await Slot().ClearAsync(Platform, Thread, id, default)).Should().BeTrue();

        (await StateOf(id)).Should().Be(SpecDialogProposalClaim.Filed);
        (await Slot().ClearAsync(Platform, Thread, id, default)).Should().BeFalse("a filed proposal is filed once");
    }

    [Fact]
    public async Task Release_WhileANewerProposalAwaits_IsRefused()
    {
        var (id, _) = await Claim().SetAwaitingAsync(Platform, Thread, "{}", default);
        await Claim().ClaimAsync(Platform, Thread, id, default);
        await Claim().SetAwaitingAsync(Platform, Thread, "{\"newer\":true}", default);

        (await Slot().ReleaseAsync(Platform, Thread, id, default)).Should().BeFalse();

        (await StateOf(id)).Should().Be(SpecDialogProposalClaim.Filing);
    }

    [Fact]
    public async Task Reject_MovesAwaitingToRejected_Once()
    {
        var (id, _) = await Claim().SetAwaitingAsync(Platform, Thread, "{}", default);

        (await Claim().RejectAsync(Platform, Thread, id, default)).Should().BeTrue();

        (await StateOf(id)).Should().Be(SpecDialogProposalClaim.Rejected);
        (await Claim().RejectAsync(Platform, Thread, id, default)).Should().BeFalse();
        (await Claim().ClaimAsync(Platform, Thread, id, default)).Should().BeNull();
    }

    [Fact]
    public async Task TwoClaimsOfOneId_OnTwoReplicas_OneWins()
    {
        var (id, _) = await Claim().SetAwaitingAsync(Platform, Thread, "{}", default);
        await using var other = MigratedStoreTemplate.Context(_connection);

        var first = await Claim().ClaimAsync(Platform, Thread, id, default);
        var second = await new SpecDialogProposalClaim(other).ClaimAsync(Platform, Thread, id, default);

        (first, second).Should().Be(("{}", (string?)null));
    }

    [Fact]
    public async Task Reviewing_NeverAwaits()
    {
        var (id, _) = await Claim().SetAwaitingAsync(Platform, Thread, "{\"awaits\":true}", "run-1", default);

        await Claim().SetReviewingAsync(Platform, Thread, "{\"draft\":true}", "run-2", default);

        (await Claim().ReadAsync(Platform, Thread, default)).AwaitingId.Should().Be(id, "a draft never takes the awaiting slot");
        var draft = await _context.Set<SpecDialogProposal>().AsNoTracking().SingleAsync(p => p.RunId == "run-2");
        (draft.State, draft.ProposalId).Should().Be((SpecDialogProposalClaim.Reviewing, (string?)null));
        (await Claim().ClaimAsync(Platform, Thread, "run-2", default)).Should().BeNull();
    }

    [Fact]
    public async Task OneAwaitingIndex_RefusesASecondAwaitingRow()
    {
        await Claim().SetAwaitingAsync(Platform, Thread, "{}", default);
        _context.Add(StoredProposals.Row(Session, "{}", "second", SpecDialogProposalClaim.Awaiting));

        var save = () => _context.SaveChangesAsync();

        await save.Should().ThrowAsync<DbUpdateException>("at most one proposal awaits per session");
    }

    // Another replica puts its proposal up between this one's supersede and its write: the later write wins, as it did on one row.
    [Fact]
    public async Task SetAwaiting_LosesTheIndexToAnotherReplica_SupersedesTheWinnerAndWrites()
    {
        var (first, _) = await Claim().SetAwaitingAsync(Platform, Thread, "{\"n\":1}", "run-1", default);
        await _context.Database.ExecuteSqlRawAsync("""
            CREATE TRIGGER other_replica AFTER UPDATE OF State ON SpecDialogProposal WHEN NEW.State = 'superseded'
            BEGIN
              INSERT OR IGNORE INTO SpecDialogProposal (SessionId, RunId, ProposalJson, ProposalId, State, CreatedAt, UpdatedAt)
              VALUES (NEW.SessionId, 'run-other', '{{}}', 'other', 'awaiting', NEW.CreatedAt, NEW.UpdatedAt);
            END;
            """);

        var (second, replaced) = await Claim().SetAwaitingAsync(Platform, Thread, "{\"n\":2}", "run-2", default);

        replaced.Should().Be("other");
        (await StateOf(first), await StateOf("other"), await StateOf(second)).Should().Be(
            (SpecDialogProposalClaim.Superseded, SpecDialogProposalClaim.Superseded, SpecDialogProposalClaim.Awaiting));
    }

    [Fact]
    public async Task TurnRows_LeaveOutTheRowsNoRunWrote()
    {
        await Claim().SetAwaitingAsync(Platform, Thread, "{\"unbound\":true}", default);
        await Claim().SetReviewingAsync(Platform, Thread, "{\"run\":true}", "run-1", default);

        var rows = await new SpecDialogProposalRepository(_context).ReadBySessionAsync(Session, default);

        rows.Should().ContainSingle().Which.RunId.Should().Be("run-1");
    }

    private SpecDialogProposalClaim Claim() => new(_context);

    private SpecDialogConfirmedSlot Slot() => new(Claim());

    private Task<string?> StateOf(string id) =>
        _context.Set<SpecDialogProposal>().AsNoTracking().Where(p => p.ProposalId == id).Select(p => p.State).SingleAsync();
}
