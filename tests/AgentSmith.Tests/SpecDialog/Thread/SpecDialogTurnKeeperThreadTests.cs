using AgentSmith.Contracts.Models;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Tests.TestHelpers;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using static AgentSmith.Tests.SpecDialog.Thread.DialogThreadStoreFixture;

namespace AgentSmith.Tests.SpecDialog.Thread;

public sealed class SpecDialogTurnKeeperThreadTests : IDisposable
{
    private const string Platform = "dashboard";
    private const string Dialog = "d-keeper";

    private readonly DialogThreadStoreFixture _store = new();

    [Fact]
    public async Task SpecDialogTurnKeeper_FailedTurn_DropsTheThread()
    {
        await using var context = _store.Context();
        var (keeper, state) = await OpenAsync(context);
        await keeper.KeepAsync(state, Result("a1", Capture("a1")), Platform, Dialog, CancellationToken.None);
        (await Rows(context)).Should().Be(1, "an answered turn keeps its thread");

        await keeper.KeepAsync(state, Result("the turn failed", Capture("a2"), SpecDialogTurnKind.Failure),
            Platform, Dialog, CancellationToken.None);

        (await Rows(context)).Should().Be(0, "a failed turn's thread is never replayed");
    }

    private static async Task<(SpecDialogTurnKeeper Keeper, ConversationState State)> OpenAsync(
        AgentSmithDbContext context)
    {
        var repository = new SpecDialogSessionRepository(context);
        var sessions = new SpecDialogSessionManager(repository, BoundSessionNudges.Quiet(repository),
            AgentSmith.Tests.Sandbox.Holds.None(), TimeProvider.System, NullLogger<SpecDialogSessionManager>.Instance);
        var opened = await sessions.OpenAsync(Platform, Dialog, Dialog, "U1",
            new ActiveScope { Project = "sample", Repos = [] }, CancellationToken.None);
        var state = await sessions.AppendTurnAsync(Platform, Dialog, TranscriptRole.User, "q1", null, null, CancellationToken.None);
        return (SilentTurnKeeper.Over(repository, sessions, context), state ?? opened);
    }

    private static Task<int> Rows(AgentSmithDbContext context) =>
        context.Set<SpecDialogThread>().AsNoTracking().CountAsync();

    public void Dispose() => _store.Dispose();
}
