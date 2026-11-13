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
    public async Task SpecDialogTurnKeeper_FailedTurn_KeepsThreadItsCallsWrote()
    {
        await using var context = _store.Context();
        var (keeper, state) = await OpenAsync(context);
        await keeper.KeepAsync(state, Result("a1", Capture("a1")), Platform, Dialog, CancellationToken.None);
        await DialogThreadStores.Over(context).KeepGrowingAsync(
            state, new ThreadSnapshot(Capture("the failed turn's call").Messages!, false, []), CancellationToken.None);

        // A Failure turn carries no thread (SpecDialogTurnEnding.OfAsync), so only what its calls wrote can be kept.
        await keeper.KeepAsync(state, Result("the turn failed", null, SpecDialogTurnKind.Failure),
            Platform, Dialog, CancellationToken.None);

        var row = await context.Set<SpecDialogThread>().AsNoTracking().SingleAsync();
        DialogThreadStores.Codec().Decode(row.MessagesJson).Select(m => m.Text)
            .Should().Equal("the prompt", "the failed turn's call");
        row.ReplyAt.Should().BeNull();
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

    public void Dispose() => _store.Dispose();
}
