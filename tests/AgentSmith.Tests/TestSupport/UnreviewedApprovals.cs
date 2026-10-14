using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Services.SpecDialog;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.TestSupport;

/// <summary>2026-10-13-8762i: the held-approval step of the outcome confirmer, over a session repository —
/// or, for a flow whose proposals are all reviewed, over none (it is never reached).</summary>
internal static class UnreviewedApprovals
{
    public static SpecDialogUnreviewedApproval Over(SpecDialogSessionRepository repository) =>
        new(new SpecDialogSessionManager(
                repository, BoundSessionNudges.Quiet(repository), Sandbox.Holds.None(), TimeProvider.System,
                NullLogger<SpecDialogSessionManager>.Instance),
            NullLogger<SpecDialogUnreviewedApproval>.Instance);

    public static SpecDialogUnreviewedApproval Unused() => Over(new SpecDialogSessionRepository(null!));
}
