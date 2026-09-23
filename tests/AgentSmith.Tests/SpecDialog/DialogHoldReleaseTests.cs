using System.Security.Claims;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Extensions;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Tests.Sandbox;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.SpecDialog;

/// <summary>
/// 2026-09-22-2d11b: a hold ends when its conversation does — closed, forked away from, or
/// deleted — over the REAL durable store. The teardown is a force remove and it is detached
/// from whoever asked for it: a fork closes the old session on the way to opening its
/// successor, and a delete runs inside the turn gate.
/// </summary>
public sealed class DialogHoldReleaseTests : IDisposable
{
    private const string Platform = "dashboard";
    private const string Owner = "person-a";
    private const string Thread = "d-1";

    private readonly SqliteConnection _connection;
    private readonly AgentSmithDbContext _context;
    private readonly SpecDialogSessionRepository _repository;
    private readonly IHeldSandboxRegister _register = Holds.Live();
    private readonly SpecDialogSessionManager _sessions;

    public DialogHoldReleaseTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _context = new AgentSmithDbContext(
            new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options);
        _context.Database.Migrate();
        _repository = new SpecDialogSessionRepository(_context);
        _sessions = new SpecDialogSessionManager(
            _repository, _register, TimeProvider.System,
            NullLogger<SpecDialogSessionManager>.Instance);
    }

    [Fact]
    public async Task Dialog_ClosingOrDeletingAConversation_ReleasesEveryHoldWithoutWaitingOutAGrace()
    {
        var mine = await OpenAsync(Thread);
        var held = Hold(mine.JobId, "repo-a");
        var second = Hold(mine.JobId, "repo-b");
        var elsewhere = Hold("another-conversation", "repo-a");

        await _sessions.CloseAsync(Platform, Thread, CancellationToken.None);

        held.ForceRemovedAt.Should().NotBeNull("a conversation that is over holds nothing");
        second.ForceRemovedAt.Should().NotBeNull("every repository it held, not the first one");
        held.Disposed.Should().BeFalse(
            "a disposal pushes a shutdown step and then waits a flat ten seconds, per sandbox");
        elsewhere.ForceRemovedAt.Should().BeNull("another conversation is still reading");
    }

    [Fact]
    public async Task Dialog_ForkingAConversation_ReleasesWhatTheClosedOneHeld()
    {
        var first = await OpenAsync(Thread);
        var held = Hold(first.JobId, "repo-a");

        var forked = await OpenAsync(Thread);

        forked.JobId.Should().NotBe(first.JobId, "opening over an open session IS the fork");
        held.ForceRemovedAt.Should().NotBeNull(
            "the successor reads its own trees; the old ones belong to nobody");
    }

    [Fact]
    public async Task Dialog_DeletingAConversation_ReleasesWhatItHeld()
    {
        var session = await OpenAsync(Thread);
        var held = Hold(session.JobId, "repo-a");
        var deleter = new Mock<ISpecDialogConversationDeleter>();

        await SpecDialogDeletionEndpoints.DeleteAsync(
            session.JobId, Principal(Owner), new SpecDialogOwnership(_repository),
            new SpecDialogTurnGate(TimeProvider.System), deleter.Object, _register,
            CancellationToken.None);

        deleter.Verify(d => d.DeleteAsync(session.JobId, It.IsAny<CancellationToken>()), Times.Once);
        held.ForceRemovedAt.Should().NotBeNull("the conversation is gone, so its sandboxes are nobody's");
    }

    [Fact]
    public async Task Dialog_AReleasedHold_IsNoLongerThereToTakeBack()
    {
        var session = await OpenAsync(Thread);
        var key = HeldSandbox.KeyFor(session.JobId, "repo-a", null);
        Hold(session.JobId, "repo-a");

        await _sessions.CloseAsync(Platform, Thread, CancellationToken.None);

        (await _register.TakeAsync(key, CancellationToken.None)).Should().BeNull();
    }

    private FakeHoldableSandbox Hold(string conversationId, string repo)
    {
        var sandbox = new FakeHoldableSandbox();
        _register.Hold(new HeldSandbox(
            HeldSandbox.KeyFor(conversationId, repo, null), conversationId, sandbox));
        return sandbox;
    }

    private Task<ConversationState> OpenAsync(string thread) =>
        _sessions.OpenAsync(Platform, "C1", thread, Owner,
            new ActiveScope { Project = "p", Repos = ["repo-a"] }, CancellationToken.None);

    private static ClaimsPrincipal Principal(string userId) =>
        new(new ClaimsIdentity([new Claim("sub", userId)], "test"));

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}
