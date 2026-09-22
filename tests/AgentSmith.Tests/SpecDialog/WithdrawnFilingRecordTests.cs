using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Services.SpecDialog;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.SpecDialog;

/// <summary>
/// 2026-09-22-9519: what the new state does to the two places a filing is READ — the notice that
/// announces it on every channel, and the stored row a later build has to reopen.
/// </summary>
public sealed class WithdrawnFilingRecordTests
{
    /// <summary>
    /// The label used to fall through to "record" for anything it had not been taught, so the
    /// first state added after it would have been announced as a slice record — which is not work
    /// at all — in the transcript, on the page and in every chat channel alike.
    /// </summary>
    [Fact]
    public void FilingNotice_AWithdrawnTicket_IsNotAnnouncedAsARecord()
    {
        var note = new FiledWorkStart(FiledStartState.Withdrawn, "closed from this conversation").Note;

        note.Should().Contain("withdrawn");
        note.Should().NotContain("record");
    }

    /// <summary>
    /// And every state names itself: two states sharing one word is the same failure wearing a
    /// different label, and it is what a sixth state added without a thought would produce.
    /// </summary>
    [Fact]
    public void FilingNotice_EveryState_HasAWordOfItsOwn()
    {
        var words = Enum.GetValues<FiledStartState>()
            .Select(state => new FiledWorkStart(state, "why").Note)
            .ToList();

        words.Should().OnlyHaveUniqueItems();
    }

    /// <summary>
    /// The rollback cost. A stored filing that cannot be read is shown as ABSENT — the pane loses
    /// the tickets, the error and the notes together — so a state name this build cannot place
    /// must cost one word and nothing else.
    /// </summary>
    [Fact]
    public void FilingRecord_AnOlderReaderMeetingTheNewState_KeepsTheRestOfTheFiling()
    {
        var session = new SpecDialogSession
        {
            ThreadId = "d-9519",
            LatestFilingJson = """
            {
              "filed": [
                { "reference": "https://tracker.test/4711", "title": "Work 4711",
                  "ticketId": "4711", "project": "alpha", "key": "#4711",
                  "start": { "state": "SomeStateThisBuildHasNeverHeardOf", "reason": "nothing would route it" } }
              ],
              "error": null,
              "at": "2026-09-22T09:00:00+00:00",
              "kind": "phase",
              "notes": ["a child the tracker would not link"]
            }
            """,
        };

        var filing = Store().Of(session).Filing;

        filing.Should().NotBeNull("one unreadable word must not take the whole filing with it");
        var ticket = filing!.Filed.Single();
        ticket.Key.Should().Be("#4711");
        ticket.TicketId.Should().Be("4711");
        filing.Kind.Should().Be("phase");
        filing.Notes.Should().Equal(["a child the tracker would not link"]);
        ticket.Start!.State.Should().BeNull("a state it cannot name is unknown, not one of the ones it can");
        ticket.Start.Reason.Should().Be("nothing would route it");
    }

    private static SpecDialogLatestOutcomeStore Store() =>
        new(new SpecDialogSessionRepository(null!), NullLogger<SpecDialogLatestOutcomeStore>.Instance);
}
