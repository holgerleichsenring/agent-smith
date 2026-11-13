using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using static AgentSmith.Tests.SpecDialog.Thread.DialogThreadStoreFixture;

namespace AgentSmith.Tests.SpecDialog.Thread;

public sealed class SpecDialogThreadStoreTests : IDisposable
{
    private readonly DialogThreadStoreFixture _store = new();

    [Fact]
    public async Task SpecDialogThreadStore_UnseenTurns_SkipTheModelsOwnReplyAndDigests()
    {
        var started = State(User("q1"));
        var kept = State(User("q1"), Assistant("an upload arrived", SpecDialogTurnKind.Notice), Assistant("a1"));
        await KeepAsync(started, Result("a1", Capture("a1")), kept);
        var later = State([.. kept.Transcript, User("q2"), Assistant("digest", SpecDialogTurnKind.Digest)]);

        await using var fresh = _store.Context();
        var thread = await DialogThreadStores.Over(fresh).LoadAsync(later, new AgentConfig(), CancellationToken.None);

        thread!.Unseen.Select(t => t.Text).Should().Equal("an upload arrived", "q2");
        thread.Messages.Select(m => m.Text).Should().Equal("the prompt", "a1");
        thread.Stewarded.Should().BeFalse();
    }

    [Fact]
    public async Task SpecDialogThreadStore_SubstitutedReply_RendersAsUnseen()
    {
        var started = State(User("q1"));
        var kept = State(User("q1"), Assistant("a notice instead"));
        await KeepAsync(started, Result("a notice instead", Capture("the model's own text")), kept);

        await using var fresh = _store.Context();
        var thread = await DialogThreadStores.Over(fresh).LoadAsync(
            State([.. kept.Transcript, User("q2")]), new AgentConfig(), CancellationToken.None);

        thread!.Unseen.Select(t => t.Text).Should().Equal("a notice instead", "q2");
    }

    [Fact]
    public async Task SpecDialogThreadStore_CompactionOff_LoadsNothing()
    {
        var kept = State(User("q1"), Assistant("a1"));
        await KeepAsync(State(User("q1")), Result("a1", Capture("a1")), kept);
        var later = State([.. kept.Transcript, User("q2")]);
        await using var fresh = _store.Context();
        var store = DialogThreadStores.Over(fresh);

        (await store.LoadAsync(later, new AgentConfig { Compaction = new CompactionConfig { IsEnabled = false } }, default))
            .Should().BeNull("a thread that cannot fold is not replayed");
        (await store.LoadAsync(later, new AgentConfig { DialogContextWindowTokens = 0 }, default))
            .Should().BeNull("nor one with no window to fold against");
        (await store.LoadAsync(kept, new AgentConfig(), default))
            .Should().BeNull("nothing new from the operator");
    }

    // 2026-11-12-a1fa: a message handed to the master with a tool result, carried by a later successful call, is seen.
    [Fact]
    public async Task SpecDialogThreadStore_DeliveredTurn_NotRenderedAsUnseen()
    {
        var started = State(User("q1"));
        var capture = Capture("a1");
        capture.Drained(1);
        capture.Record([new(Microsoft.Extensions.AI.ChatRole.User, "the prompt")],
            new(new Microsoft.Extensions.AI.ChatMessage(Microsoft.Extensions.AI.ChatRole.Assistant, "a1")));
        var kept = State(User("q1"), User("also this"), Assistant("a1"));
        await KeepAsync(started, Result("a1", capture), kept);
        var later = State([.. kept.Transcript, User("q2")]);

        await using var fresh = _store.Context();
        var thread = await DialogThreadStores.Over(fresh).LoadAsync(later, new AgentConfig(), CancellationToken.None);

        thread!.Unseen.Select(t => t.Text).Should().Equal("q2");
    }

    // 2026-11-12-a1fa: drained into a turn whose next call never answered, the message was never read — so it renders.
    [Fact]
    public async Task SpecDialogThreadStore_DrainedThenFailed_RenderedAgain()
    {
        var started = State(User("q1"));
        var capture = Capture("half way");
        capture.Drained(1);
        await using (var context = _store.Context())
            await DialogThreadStores.Over(context).KeepGrowingAsync(
                started, new ThreadSnapshot(capture.Messages!, false, capture.Delivered), CancellationToken.None);
        var later = State(User("q1"), User("also this"), Assistant("the turn failed", SpecDialogTurnKind.Failure));

        await using var fresh = _store.Context();
        var thread = await DialogThreadStores.Over(fresh).LoadAsync(later, new AgentConfig(), CancellationToken.None);

        thread!.Unseen.Select(t => t.Text).Should().Equal("also this", "the turn failed");
    }

    [Fact]
    public async Task SpecDialogThreadStore_Load_MidTurnRow_RendersFromTranscriptFrom()
    {
        var started = State(User("q1"), Assistant("a1"), User("q2"));
        await using (var context = _store.Context())
            await DialogThreadStores.Over(context).KeepGrowingAsync(
                started, new ThreadSnapshot(Capture("half way").Messages!, true, []), CancellationToken.None);
        var later = State([.. started.Transcript, Assistant("the turn failed", SpecDialogTurnKind.Failure), User("q3")]);

        await using var fresh = _store.Context();
        var thread = await DialogThreadStores.Over(fresh).LoadAsync(later, new AgentConfig(), CancellationToken.None);

        thread!.Messages.Select(m => m.Text).Should().Equal("the prompt", "half way");
        thread.Unseen.Select(t => t.Text).Should().Equal("the turn failed", "q3");
        thread.Stewarded.Should().BeTrue();
    }

    [Fact]
    public void SpecDialogThreadStore_CanFold_FollowsCompactionAndWindow()
    {
        using var context = _store.Context();
        var store = DialogThreadStores.Over(context);

        store.CanFold(new AgentConfig()).Should().BeTrue();
        store.CanFold(new AgentConfig { Compaction = new CompactionConfig { IsEnabled = false } }).Should().BeFalse();
    }

    [Fact]
    public async Task SpecDialogThreadRepository_TwoWriters_NewestByIdWinsAndOlderAreDeleted()
    {
        await using var first = _store.Context();
        await using var second = _store.Context();
        await new SpecDialogThreadRepository(first).ReplaceAsync(Row("one"), default);
        await new SpecDialogThreadRepository(second).ReplaceAsync(Row("two"), default);
        await new SpecDialogThreadRepository(first).ReplaceAsync(Row("three"), default);

        await using var read = _store.Context();
        (await new SpecDialogThreadRepository(read).NewestAsync(Session, default))!.MessagesJson.Should().Be("three");
        (await read.Set<SpecDialogThread>().CountAsync()).Should().Be(1);
    }

    private async Task KeepAsync(
        ConversationState started, SpecDialogTurnResult result, ConversationState kept)
    {
        await using var context = _store.Context();
        await DialogThreadStores.Over(context).KeepAsync(started, result, kept, CancellationToken.None);
    }

    private static SpecDialogThread Row(string json) => new() { SessionId = Session, MessagesJson = json };

    public void Dispose() => _store.Dispose();
}
