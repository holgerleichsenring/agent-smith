using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using static AgentSmith.Tests.SpecDialog.Thread.DialogThreadStoreFixture;

namespace AgentSmith.Tests.SpecDialog.Thread;

public sealed class SpecDialogThreadWriterTests : IDisposable
{
    private readonly DialogThreadStoreFixture _store = new();
    private readonly List<string> _written = [];

    [Fact]
    public async Task SpecDialogThreadWriter_EachCall_ReplacesSnapshot_OneWriteInFlight()
    {
        var release = new TaskCompletionSource();
        var firstStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var inFlight = 0;
        var mostInFlight = 0;
        var writer = Writer(true, async (messages, _) =>
        {
            mostInFlight = Math.Max(mostInFlight, Interlocked.Increment(ref inFlight));
            firstStarted.TrySetResult();
            await release.Task;
            lock (_written) _written.Add(messages[^1].Text);
            Interlocked.Decrement(ref inFlight);
        });

        writer.Offer(Thread("call 1"), false);
        await firstStarted.Task;
        writer.Offer(Thread("call 2"), false);
        writer.Offer(Thread("call 3"), false);
        release.SetResult();
        await writer.CloseAsync();

        mostInFlight.Should().Be(1);
        _written.Should().Equal("call 1", "call 3");
    }

    [Fact]
    public async Task SpecDialogThreadWriter_Closed_NoWriteAfterFinal()
    {
        var writer = Writer(true, Recording);
        writer.Offer(Thread("call 1"), false);

        await writer.CloseAsync();
        writer.Offer(Thread("call 2"), false);
        await writer.CloseAsync();

        _written.Should().Equal("call 1");
    }

    [Fact]
    public async Task SpecDialogThreadWriter_CannotFold_WritesNothing()
    {
        var writer = Writer(false, Recording);

        writer.Offer(Thread("call 1"), false);
        await writer.CloseAsync();

        _written.Should().BeEmpty("Load would not replay a thread the turn cannot fold");
    }

    [Fact]
    public async Task SpecDialogThreadWriteLink_Observe_CaptureWritesTheGrowingThreadInItsOwnScope()
    {
        var services = new ServiceCollection().AddLogging()
            .AddScoped<IUnitOfWork>(_ => _store.Context())
            .AddScoped(p => DialogThreadStores.Over(p.GetRequiredService<IUnitOfWork>())).BuildServiceProvider();
        await using var turn = services.CreateAsyncScope();
        var capture = new ForwardedThreadCapture();
        capture.Lead(true);
        var writer = new SpecDialogThreadWriteLink(services.GetRequiredService<IServiceScopeFactory>(),
            turn.ServiceProvider.GetRequiredService<SpecDialogThreadStore>(), NullLoggerFactory.Instance)
            .Observe(State(User("q1")), new AgentConfig(), capture);

        capture.Record([new ChatMessage(ChatRole.User, "the prompt")], new ChatResponse(new ChatMessage(ChatRole.Assistant, "a tool call")));
        await writer.CloseAsync();

        await using var read = _store.Context();
        var row = await new SpecDialogThreadRepository(read).NewestAsync(Session, default);
        row!.Stewarded.Should().BeTrue("the master the loop started with travels with the sink");
        row.TranscriptFrom.Should().Be(1);
        row.ReplyAt.Should().BeNull("a running turn has no reply yet");
        DialogThreadStores.Codec().Decode(row.MessagesJson).Select(m => m.Text).Should().Equal("the prompt", "a tool call");
    }

    private Task Recording(IReadOnlyList<ChatMessage> messages, bool stewarded)
    {
        lock (_written) _written.Add(messages[^1].Text);
        return Task.CompletedTask;
    }

    private static SpecDialogThreadWriter Writer(bool isFoldable, Func<IReadOnlyList<ChatMessage>, bool, Task> write) =>
        new(isFoldable, write, NullLogger.Instance);

    private static IReadOnlyList<ChatMessage> Thread(string last) =>
        [new ChatMessage(ChatRole.User, "the prompt"), new ChatMessage(ChatRole.Assistant, last)];

    public void Dispose() => _store.Dispose();
}
