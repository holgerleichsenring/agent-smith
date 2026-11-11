using AgentSmith.Contracts.Models;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace AgentSmith.Tests.SpecDialog;

/// <summary>
/// 2026-11-11-205fh: a typed decision in a chat thread binds by the platform's own clock or not at all. Slack's ts on
/// both sides; Teams gives no time for a posted card, so its buttons decide; a thread whose card post failed is shown
/// the card again instead of the word becoming a model turn.
/// </summary>
public sealed class ChatCardBindingTests : IDisposable
{
    private const string Thread = "th-205fh";

    private readonly SqliteConnection _connection;
    private readonly AgentSmithDbContext _context;
    private readonly Mock<IOutcomeSink> _sink = new();

    public ChatCardBindingTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _context = new AgentSmithDbContext(new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options);
        MigratedStoreTemplate.CopyInto(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task TypedApprove_ServerClockAheadOfSlack_StillDecides()
    {
        // Slack's clock runs 10 s behind the server's; the word is typed 2 s after Slack posted the card.
        var slackCardAt = DateTimeOffset.UtcNow.AddSeconds(-10);
        var slack = new RecordingThreadAdapter("slack") { PostedAt = () => slackCardAt };
        var (harness, state) = await ProposedAsync(slack);

        var decided = await harness.Decisions().DecideAsync(
            state, SpecDialogDecision.Approved, null, false, default, sentAt: slackCardAt.AddSeconds(2));

        decided.Should().Be(SpecDialogDecided.Decided, "both times are Slack's, so the word came after the card");
    }

    [Fact]
    public async Task TypedApprove_Teams_DecidesNothing()
    {
        var teams = new RecordingThreadAdapter("teams") { PostedAt = () => null };
        var (harness, state) = await ProposedAsync(teams, "teams");

        var decided = await harness.Decisions().DecideAsync(
            state, SpecDialogDecision.Approved, null, false, default, sentAt: DateTimeOffset.UtcNow.AddMinutes(1));

        decided.Should().Be(SpecDialogDecided.No, "a Teams card is decided by its buttons");
        (await harness.Outcomes.ReadAsync("teams", Thread, default)).ProposalId.Should().NotBeNull("the card still awaits");
    }

    [Fact]
    public async Task TypedApprove_CardPostFailed_RepostsTheCardAndRunsNoTurn()
    {
        var slack = new RecordingThreadAdapter("slack") { Fails = true };
        var (harness, state) = await ProposedAsync(slack);
        slack.Fails = false;

        var decided = await harness.Decisions().DecideAsync(
            state, SpecDialogDecision.Approved, null, false, default, sentAt: DateTimeOffset.UtcNow);

        decided.Should().Be(SpecDialogDecided.Replaced, "a Replaced decision returns before the router starts a turn");
        slack.Questions.Should().ContainSingle("the card the thread was never shown is posted now");
        var transcript = (await harness.Sessions.GetOpenByThreadAsync("slack", Thread, default))!.Transcript;
        transcript.Should().Contain(t => t.Kind == SpecDialogTurnKind.Notice && t.Text.Contains("never shown"));
        _sink.Invocations.Should().BeEmpty("nothing was decided");
    }

    private async Task<(SpecDialogDecisionHarness Harness, ConversationState State)> ProposedAsync(
        RecordingThreadAdapter adapter, string platform = "slack")
    {
        var harness = new SpecDialogDecisionHarness(_context, sink: _sink.Object, threads: [adapter]);
        var state = await harness.OpenAsync(platform, Thread);
        await harness.Flow().HandleAsync(state, Spec("p1"), default);
        return (harness, (await harness.Sessions.GetOpenByThreadAsync(platform, Thread, default))!);
    }

    private static SpecOutcome Spec(string id) => new(new SpecDraft(id, $"goal of {id}", $"spec: {id}", []));
}
