using System.Net;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Services.Providers.Tickets;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.VisualStudio.Services.Common;

namespace AgentSmith.Tests.Providers.Tickets;

/// <summary>
/// 2026-09-18-c1a7: finalize stops being a call whose success says nothing. Three of the four
/// trackers answer from their own vocabulary before the write; only Azure DevOps learns it from
/// a rejection, and it classifies on the exception TYPE, never on message text.
/// </summary>
public sealed class TicketFinalizeReportTests
{
    private static readonly TicketId Ticket = new("42");

    // On GitHub the two writes are unchanged — the comment is the run's one word to a human and
    // the label is the mark the operator configured. Only the ANSWER is new: a label is not the
    // issue's state, so the ticket is still open and the next poll would claim it again.
    [Fact]
    public async Task Finalize_ProviderThatCannotExpressTheStatus_StillPostsTheCommentAndSaysSo()
    {
        var posted = new List<string>();
        var labelled = new List<string>();
        var finalizer = new GitHubTicketFinalizer(
            comment: (_, body, _) => { posted.Add(body); return Task.CompletedTask; },
            close: (_, _, _) => throw new InvalidOperationException("close is not this path"),
            transition: (_, status, _) => { labelled.Add(status); return Task.CompletedTask; });

        var result = await finalizer.FinalizeAsync(Ticket, "summary", "needs-triage", CancellationToken.None);

        result.StatusMoved.Should().BeFalse();
        result.Outcome.Should().Be(TicketFinalizeOutcome.StatusNotExpressible);
        result.RequestedStatus.Should().Be("needs-triage");
        // Equal(params) would read a reason string as another expected element — pass the list.
        posted.Should().Equal(["summary"], "the failed run's one comment must still reach the ticket");
        labelled.Should().Equal(["needs-triage"], "the label an operator configured is still applied");
    }

    // On GitLab the state write can only 400, so it is not sent — but the comment still is.
    [Fact]
    public async Task Finalize_GitLabStatusOutsideTheStateVocabulary_PostsTheCommentAndSkipsTheStateWrite()
    {
        var posted = new List<string>();
        var applied = new List<string>();
        var finalizer = new GitLabTicketFinalizer(
            comment: (_, body, _) => { posted.Add(body); return Task.CompletedTask; },
            close: (_, _, _) => throw new InvalidOperationException("close is not this path"),
            transition: (_, status, _) => { applied.Add(status); return Task.CompletedTask; });

        var result = await finalizer.FinalizeAsync(Ticket, "summary", "Failed", CancellationToken.None);

        result.Outcome.Should().Be(TicketFinalizeOutcome.StatusNotExpressible);
        posted.Should().Equal(["summary"]);
        applied.Should().BeEmpty("a state_event GitLab does not take is not worth the request");
    }

    [Fact]
    public async Task Finalize_ProviderWithNoTransitionToTheConfiguredName_ReportsTheTicketUnmoved()
    {
        var result = await Jira().FinalizeAsync(Ticket, "summary", "Rejected", CancellationToken.None);

        result.StatusMoved.Should().BeFalse();
        result.Outcome.Should().Be(TicketFinalizeOutcome.NoTransitionToTheStatus);
        result.RequestedStatus.Should().Be("Rejected");
    }

    // The close path is a transition too: with no configured status the tracker's default is
    // asked for, and a workflow that offers none leaves the issue exactly as open.
    [Fact]
    public async Task Finalize_CloseWithoutATransitionToTheDefaultStatus_ReportsTheTicketUnmoved()
    {
        var result = await Jira().FinalizeAsync(Ticket, "summary", null, CancellationToken.None);

        result.StatusMoved.Should().BeFalse();
        result.Outcome.Should().Be(TicketFinalizeOutcome.NoTransitionToTheStatus);
        result.RequestedStatus.Should().Be("Done");
    }

    private static JiraTicketFinalizer Jira() => new(
        "Done",
        comment: (_, _, _) => Task.CompletedTask,
        close: (_, _, _) => Task.FromResult(false),
        transition: (_, _, _) => Task.FromResult(false));

    [Fact]
    public async Task Finalize_TrackerRejectsTheValue_ReportsTheTicketUnmoved()
    {
        var result = await Rejecting(new RuleValidationException("rule error for field State", null!));

        result.StatusMoved.Should().BeFalse();
        result.Outcome.Should().Be(TicketFinalizeOutcome.TrackerRejectedTheStatus);
        result.RequestedStatus.Should().Be("Failed");
    }

    // A throttle, a 503 or a rotated token is a failure that may pass. Recording it would refuse
    // every later claim over a status name that is perfectly correct, so it propagates instead.
    [Fact]
    public async Task Finalize_TrackerFailsForAReasonThatMayPass_IsNotClassifiedAsARejection()
    {
        var act = () => Rejecting(new VssServiceException("VS800075: service unavailable"));

        await act.Should().ThrowAsync<VssServiceException>();
    }

    [Fact]
    public async Task Finalize_StatusMoved_ReportsItMoved()
    {
        var applied = new List<string>();
        var finalizer = new GitLabTicketFinalizer(
            comment: (_, _, _) => Task.CompletedTask,
            close: (_, _, _) => Task.CompletedTask,
            transition: (_, status, _) => { applied.Add(status); return Task.CompletedTask; });

        var result = await finalizer.FinalizeAsync(Ticket, "summary", "closed", CancellationToken.None);

        result.StatusMoved.Should().BeTrue();
        result.Outcome.Should().Be(TicketFinalizeOutcome.Moved);
        applied.Should().Equal("closed");
    }

    [Fact]
    public async Task Finalize_Classification_NeverMatchesOnMessageText()
    {
        var wordy = await Rejecting(
            new RuleValidationException("TF401320: the field State does not allow 'Failed'", null!));
        var silent = await Rejecting(new RuleValidationException("TF401320", null!));

        wordy.Outcome.Should().Be(silent.Outcome,
            "the classification reads the exception type, so the wording cannot change it");
        wordy.Detail.Should().Be(nameof(RuleValidationException));
        silent.Detail.Should().Be(nameof(RuleValidationException));
    }

    // Jira's commonest finalize failure is a transition screen demanding a field the run cannot
    // fill: the POST answers 400, which is the same fact as a false — the issue did not move.
    [Fact]
    public async Task Finalize_JiraRefusesTheTransitionWithBadRequest_ReportsTheTicketUnmoved()
    {
        var finalizer = new JiraTicketFinalizer(
            "Done",
            comment: (_, _, _) => Task.CompletedTask,
            close: (_, _, _) => Task.FromResult(true),
            transition: (_, _, _) => throw new HttpRequestException(
                "HTTP 400 Bad Request", null, HttpStatusCode.BadRequest));

        var result = await finalizer.FinalizeAsync(Ticket, "summary", "Rejected", CancellationToken.None);

        result.Outcome.Should().Be(TicketFinalizeOutcome.TrackerRejectedTheStatus);
        result.RequestedStatus.Should().Be("Rejected");
    }

    [Fact]
    public async Task Finalize_JiraFailsForAReasonThatMayPass_IsNotClassifiedAsARejection()
    {
        var finalizer = new JiraTicketFinalizer(
            "Done",
            comment: (_, _, _) => Task.CompletedTask,
            close: (_, _, _) => Task.FromResult(true),
            transition: (_, _, _) => throw new HttpRequestException(
                "HTTP 503", null, HttpStatusCode.ServiceUnavailable));

        var act = () => finalizer.FinalizeAsync(Ticket, "summary", "Rejected", CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    // A patch that is never sent must not be reported as a move: an Azure DevOps work item id is
    // a number, and the write silently did nothing for anything else.
    [Fact]
    public async Task Finalize_AzureDevOpsIdThatIsNotAWorkItemId_IsNotReportedAsAMove()
    {
        var connection = new AzureDevOpsTicketConnection("https://dev.azure.com/org", "Project", "token");
        var provider = new AzureDevOpsTicketProvider(
            connection,
            new AzureDevOpsAttachmentLoader(connection, new HttpClient(), NullLogger.Instance),
            new AzureDevOpsFieldMapper(), NullLogger<AzureDevOpsTicketProvider>.Instance);

        var act = () => provider.FinalizeAsync(
            new TicketId("not-an-id"), "summary", "Closed", CancellationToken.None);

        await act.Should().ThrowAsync<TicketNotFoundException>();
    }

    private static Task<TicketFinalizeResult> Rejecting(Exception refusal) =>
        new AzureDevOpsTicketFinalizer("Closed", (_, _, _, _) => throw refusal, NullLogger.Instance)
            .FinalizeAsync(Ticket, "summary", "Failed", CancellationToken.None);
}
