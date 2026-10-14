using System.Net;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Domain.Models;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.Tickets.Text;
using FluentAssertions;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;

namespace AgentSmith.Tests.Tickets;

/// <summary>A failed ticket read is told as a class per tracker client, never as the exception's text.</summary>
public sealed class TicketReadFailureReasonTests
{
    public static TheoryData<Exception, string> Failures() => new()
    {
        { new TicketNotFoundException(new TicketId("42")), TicketTextReasons.NotFound },
        { new HttpRequestException("gone", null, HttpStatusCode.NotFound), TicketTextReasons.NotFound },
        { new Octokit.NotFoundException("gone", HttpStatusCode.NotFound), TicketTextReasons.NotFound },
        { new VssServiceException("TF401232: Work item 42 does not exist, or you do not have permissions to read it."), TicketTextReasons.NotFound },
        { new HttpRequestException("no", null, HttpStatusCode.Unauthorized), TicketTextReasons.Unauthorized },
        { new HttpRequestException("no", null, HttpStatusCode.Forbidden), TicketTextReasons.Unauthorized },
        { new VssUnauthorizedException("no"), TicketTextReasons.Unauthorized },
        { new Octokit.AuthorizationException(), TicketTextReasons.Unauthorized },
        { new HttpRequestException("down at https://tracker.internal"), TicketTextReasons.Unreachable },
        { new VssServiceException("TF400898: An internal error occurred."), TicketTextReasons.Unreachable },
        { new TaskCanceledException("timeout"), TicketTextReasons.Unreachable },
    };

    [Theory]
    [MemberData(nameof(Failures))]
    public void TicketText_Unavailable_ClassifiesNotFoundAndUnauthorizedPerTracker(Exception failure, string reason) =>
        TicketReadFailureReason.Of(failure).Should().Be(reason);
}
