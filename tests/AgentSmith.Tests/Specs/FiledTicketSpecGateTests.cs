using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Specs;
using AgentSmith.Contracts.Tickets;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Specs;

/// <summary>2026-11-27-3532a: a run its series started (SeriesAutoStart) had the approval record read when it started, so the
/// record decides whether the set is missing — the approved-set stamp on the ticket does not.</summary>
public sealed class FiledTicketSpecGateTests
{
    private static readonly TicketLabelVocabulary Vocabulary = TicketLabelVocabulary.For(new TrackerConnection { Type = TrackerType.GitHub });

    private static readonly Ticket Stamped =
        new(new TicketId("1"), "A ticket", "Body", null, "open", "github", [Vocabulary.ApprovedSetStamp]);

    [Fact]
    public void MissingSet_SeriesStartedStampNoRecord_Derives() =>
        Gate().MissingSet(Stamped, new TicketKey("github-1"), record: null, SpecSetBranchState.NothingAtThePath, Vocabulary, seriesStarted: true)
            .Should().BeNull("the record decides for a series-started run, and there is none");

    [Fact]
    public void MissingSet_TriggeredStampNoRecord_StillParks() =>
        Gate().MissingSet(Stamped, new TicketKey("github-1"), record: null, SpecSetBranchState.NothingAtThePath, Vocabulary)
            .Should().NotBeNull("a run nobody started by its series still reads the stamp");

    private static FiledTicketSpecGate Gate() => new(NullLogger<FiledTicketSpecGate>.Instance);
}
