using AgentSmith.Contracts.Dialogue;
using AgentSmith.Infrastructure.Services.Bus;
using AgentSmith.Infrastructure.Services.Dialogue;
using AgentSmith.Server.Contracts;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.Adapters;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Tests.Server;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.SpecDialog;

/// <summary>
/// A conversation's question stream outlives every turn, so a turn relays only what is asked
/// after it started. The REAL bus and the REAL dialogue transport over one in-memory stream.
/// </summary>
public sealed class SpecDialogQuestionPumpTests
{
    private const string Session = "s-1";

    private readonly FakeRedisStreams _redis = new();
    private readonly RecordingDialogHub _hub = new();
    private readonly SpecDialogPendingQuestions _pending =
        new(new SpecDialogTurnGate(TimeProvider.System));
    private readonly RedisDialogueTransport _transport;
    private readonly SpecDialogQuestionPump _pump;

    public SpecDialogQuestionPumpTests()
    {
        _transport = new RedisDialogueTransport(_redis.Connection, NullLogger<RedisDialogueTransport>.Instance);
        var messenger = new SpecDialogMessenger(
            [new DashboardAdapter(NullLogger<DashboardAdapter>.Instance, _hub)],
            NullLogger<SpecDialogMessenger>.Instance);
        _pump = new SpecDialogQuestionPump(
            new RedisMessageBus(_redis.Connection, NullLogger<RedisMessageBus>.Instance),
            messenger, _pending, new SpecDialogReplyComposer(), NullLogger<SpecDialogQuestionPump>.Instance);
    }

    [Fact]
    public async Task StartAsync_EarlierTurnsQuestionOnTheStream_IsNotRelayed()
    {
        await AskAsync("q-old", "Which repo did you mean?");
        using var stop = new CancellationTokenSource();

        var pumping = await _pump.StartAsync(State(), stop.Token);
        await AskAsync("q-new", "Should the cache expire?");
        await TestWaits.UntilAsync(() => Posted().Any(), "the new question is relayed");
        await stop.CancelAsync();
        await pumping;

        Posted().Should().ContainSingle().Which.Should().Contain("Should the cache expire?",
            "the earlier turn's question was answered there and is not asked again");
        _pending.TryPeek(Session, out var pending).Should().BeTrue();
        pending.Question.QuestionId.Should().Be("q-new");
    }

    [Fact]
    public async Task StartAsync_QuestionPublishedAfterStart_IsRelayedAndPending()
    {
        using var stop = new CancellationTokenSource();

        var pumping = await _pump.StartAsync(State(), stop.Token);
        await AskAsync("q-1", "Keep the old endpoint?");
        await TestWaits.UntilAsync(() => _pending.TryPeek(Session, out _), "the question is set pending");
        await TestWaits.UntilAsync(() => Posted().Any(), "the question is posted");
        await stop.CancelAsync();
        await pumping;

        Posted().Single().Should().Contain("Keep the old endpoint?");
        _pending.TryPeek(Session, out var pending).Should().BeTrue();
        pending.Question.QuestionId.Should().Be("q-1");
    }

    private Task AskAsync(string id, string text) =>
        _transport.PublishQuestionAsync(Session,
            new DialogQuestion(id, QuestionType.FreeText, text, null, null, null, TimeSpan.Zero),
            CancellationToken.None);

    private IEnumerable<string> Posted() =>
        _hub.Pushes.SelectMany(push => push.Args.OfType<SpecDialogChannelMessage>()).Select(m => m.Text);

    private static ConversationState State() => new()
    {
        JobId = Session, ChannelId = "d-1", ThreadId = "d-1", UserId = "U1", Platform = "dashboard",
        Project = "sample", TicketId = string.Empty, StartedAt = DateTimeOffset.UtcNow,
    };
}
