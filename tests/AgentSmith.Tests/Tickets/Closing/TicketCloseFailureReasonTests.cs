using System.Net;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.Tickets.Closing;
using FluentAssertions;

namespace AgentSmith.Tests.Tickets.Closing;

/// <summary>
/// 2026-10-15-114fa: a host that answered a close with a status other than 404, 401 or 403 declined it — refused,
/// not unreachable; the read classes stand for the rest.
/// </summary>
public sealed class TicketCloseFailureReasonTests
{
    public static TheoryData<Exception, string> Failures() => new()
    {
        { new Octokit.ApiException("Validation Failed", (HttpStatusCode)422), TicketCloseReasons.Refused },
        { new HttpRequestException("locked", null, HttpStatusCode.MethodNotAllowed), TicketCloseReasons.Refused },
        { new HttpRequestException("conflict", null, HttpStatusCode.Conflict), TicketCloseReasons.Refused },
        { new Octokit.NotFoundException("gone", HttpStatusCode.NotFound), TicketTextReasons.NotFound },
        { new HttpRequestException("no", null, HttpStatusCode.Forbidden), TicketTextReasons.Unauthorized },
        { new HttpRequestException("down at https://host.internal"), TicketTextReasons.Unreachable },
    };

    [Fact]
    public void TicketCloseFailureReason_HostStatus422_IsRefusedAnd404_IsNotFound()
    {
        TicketCloseFailureReason.Of(new Octokit.ApiException("Validation Failed", (HttpStatusCode)422)).Should().Be(TicketCloseReasons.Refused);
        TicketCloseFailureReason.Of(new HttpRequestException("gone", null, HttpStatusCode.NotFound)).Should().Be(TicketTextReasons.NotFound);
    }

    [Theory]
    [MemberData(nameof(Failures))]
    public void TicketCloseFailureReason_EachHostShape_IsItsClass(Exception failure, string reason) =>
        TicketCloseFailureReason.Of(failure).Should().Be(reason);
}
