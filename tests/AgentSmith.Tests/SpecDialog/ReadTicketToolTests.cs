using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.SpecDialog;

/// <summary>
/// 2026-09-27-481ba: reading the ticket a conversation is bound to, when the seeded copy was
/// capped.
/// <para>
/// Before this, the prompt told the model to SAY SO if the answer depended on the rest — an
/// instruction to report a limitation with no way to overcome it. The read re-reads the tracker,
/// because the part that did not fit is stored nowhere: the ticket is read once at binding and
/// what is kept is the already-capped text in a column bounded at the same number.
/// </para>
/// </summary>
public sealed class ReadTicketToolTests
{
    [Fact]
    public async Task ReadTicketTool_AskedFromAPosition_AnswersASliceAndWhatRemains()
    {
        var body = new string('x', 20_000);
        var reader = Reader(body);

        var first = await reader.ReadAsync(0, CancellationToken.None);
        var next = await reader.ReadAsync(first.Text.Length, CancellationToken.None);

        first.Text.Should().HaveLength(BoundTicketReader.Slice);
        first.Remaining.Should().BeGreaterThan(0);
        next.From.Should().Be(first.Text.Length);
        // The two slices are contiguous, so a caller paging through never skips or repeats.
        (first.Text + next.Text).Should().StartWith(first.Text);
    }

    [Fact]
    public async Task ReadTicketTool_ReadPastTheEnd_AnswersNothingAndNoRemainder()
    {
        var answer = await Reader("short").ReadAsync(100_000, CancellationToken.None);

        answer.Text.Should().BeEmpty();
        answer.Remaining.Should().Be(0);
        answer.Reason.Should().BeNull("there is nothing wrong with having read it all");
    }

    [Fact]
    public async Task ReadTicketTool_ATrackerThatCannotBeReached_AnswersTheReasonAndNeverThrows()
    {
        var reader = new BoundTicketReader(
            new Failing(), Project(), "412", NullLogger.Instance);

        var answer = await reader.ReadAsync(0, CancellationToken.None);

        answer.Reason.Should().NotBeNullOrWhiteSpace();
        answer.Text.Should().BeEmpty();
    }

    [Fact]
    public void ReadTicketTool_TheToolItOffers_IsNamedForWhatItDoes()
    {
        var tools = new ReadTicketToolHost(Reader("short")).GetTools(null, null).ToList();

        tools.Should().ContainSingle().Which.Name.Should().Be("read_ticket");
    }

    private static BoundTicketReader Reader(string description) =>
        new(new OneTicket(description), Project(), "412", NullLogger.Instance);

    private static ResolvedProject Project() => new()
    {
        Name = "sample",
        Tracker = new TrackerConnection { Name = "jira-main", Type = TrackerType.Jira },
    };

    private sealed class OneTicket(string description) : ITicketProviderFactory
    {
        public ITicketProvider Create(TrackerConnection config) =>
            new StubTicketProvider(id => new Ticket(
                id, "Widget drops", description, null, "Open", "Jira"));

        public ITicketRewriter CreateRewriter(TrackerConnection config) => new RecordingTicketRewriter();

        public ITicketSearch CreateSearch(TrackerConnection config) => new RecordingTicketSearch();

        public ITicketLinkedWork CreateLinkedWork(TrackerConnection config) =>
            new RecordingLinkedWork();
    }

    private sealed class Failing : ITicketProviderFactory
    {
        public ITicketProvider Create(TrackerConnection config) =>
            new StubTicketProvider(id => throw new TicketNotFoundException(id));

        public ITicketRewriter CreateRewriter(TrackerConnection config) => new RecordingTicketRewriter();

        public ITicketSearch CreateSearch(TrackerConnection config) => new RecordingTicketSearch();

        public ITicketLinkedWork CreateLinkedWork(TrackerConnection config) =>
            new RecordingLinkedWork();
    }
}
