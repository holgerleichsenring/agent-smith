using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Models;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Server.Services.SpecDialog.Continuation;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Tests.SpecDialog.Continuation;

/// <summary>
/// 2026-10-28-7a69c: a ticket's conversation can end in a comment the person approves — posted on
/// the ticket, marked as ours, and nothing recorded or started.
/// </summary>
public sealed class TicketCommentOutcomeTests : IDisposable
{
    private static readonly TicketCommentOutcome Proposal = new("Needs a decision from the platform team:\nshould the widget retry?");
    private readonly ContinuationHarness _h = new();

    public void Dispose() => _h.Dispose();

    [Fact]
    public void CommentOutcome_Parsed_CarriesTheTrimmedComment() =>
        new TicketCommentOutcomeParser().Parse(Map("  hold this until the API ships  "))
            .Should().BeOfType<OutcomeResolved>().Which.Proposal.Should().BeOfType<TicketCommentOutcome>()
            .Which.Comment.Should().Be("hold this until the API ships");

    [Fact]
    public void CommentOutcome_Missing_IsInvalidNamingTheField()
    {
        new TicketCommentOutcomeParser().Parse(Map(null)).Should().BeOfType<OutcomeInvalid>()
            .Which.Error.Should().Contain("'comment'");
        new TicketCommentOutcomeParser().Parse(Map(new string('x', TicketCommentOutcomeParser.MaxLength + 1)))
            .Should().BeOfType<OutcomeInvalid>().Which.Error.Should().Contain("at most 4000");
    }

    [Fact]
    public void CommentOutcome_StoredJson_RoundTrips()
    {
        var json = OutcomeProposalJson.Write(Proposal);

        json.Should().Contain("\"kind\":\"comment\"");
        OutcomeProposalJson.Read(json).Should().BeOfType<TicketCommentOutcome>().Which.Comment.Should().Be(Proposal.Comment);
    }

    [Fact]
    public async Task Continuation_CommentApproved_PostsOnTheTicketAndStartsNothing()
    {
        await _h.BindAsync(Proposal);

        await _h.AcceptAsync(Proposal, mayStartRuns: true);

        var (ticket, body) = _h.Tracker.Comments.Should().ContainSingle().Subject;
        ticket.Should().Be(ContinuationHarness.TicketId);
        body.Should().StartWith(TicketCommentPost.Heading).And.Contain("platform team:<br/>should the widget retry?");
        _h.Tracker.Created.Should().BeEmpty();
        (await _h.RecordAsync()).Should().BeNull("a comment records no series under the ticket");
        (await _h.NudgesAsync()).Should().BeEmpty("nothing starts");
        (await _h.OpenProposalJsonAsync()).Should().BeNull("an acted-on proposal is not offered again");
    }

    [Fact]
    public async Task Sink_CommentInAnUnboundConversation_IsRefused()
    {
        await _h.BindAsync(Proposal);

        await _h.InScopeAsync(async sp =>
        {
            await sp.GetRequiredService<TicketFilingOutcomeSink>()
                .AcceptAsync(ContinuationHarness.State() with { TicketKey = null }, Proposal, false, default);
            return true;
        });

        _h.Tracker.Comments.Should().BeEmpty();
        _h.Tracker.Created.Should().BeEmpty("a comment is never filed as a ticket");
    }

    [Fact]
    public async Task Preview_CommentProposal_HasNoStartArm()
    {
        await _h.BindAsync(Proposal);

        var view = (await _h.PreviewAsync(mayStartRuns: true))!.View!;

        view.Effect.Should().Be(TicketContinuationEffects.Comment);
        view.Start.Should().BeNull();
    }

    [Fact]
    public void Render_EscapesEachLineAndJoinsThemWithBreaks() =>
        TicketCommentPost.Render("a <b>bold</b> claim\nsecond").Should()
            .Be($"{TicketCommentPost.Heading}<br/><br/>a &lt;b&gt;bold&lt;/b&gt; claim<br/>second");

    private static IReadOnlyDictionary<string, object?> Map(string? comment) =>
        new Dictionary<string, object?> { ["kind"] = "comment", ["comment"] = comment };
}
