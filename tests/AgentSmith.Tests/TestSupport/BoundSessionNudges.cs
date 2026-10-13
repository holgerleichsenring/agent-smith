using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Services.SpecDialog;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.TestSupport;

/// <summary>2026-10-13-9a59g: the bound-session save over a repository, nudging a given ticket-activity
/// port or none, for tests whose subject is the conversation rather than the nudge.</summary>
internal static class BoundSessionNudges
{
    public static BoundSessionNudge Quiet(SpecDialogSessionRepository repository, ITicketActivityNudge? activity = null) =>
        new(repository, activity ?? Mock.Of<ITicketActivityNudge>(), NullLogger<BoundSessionNudge>.Instance);
}
