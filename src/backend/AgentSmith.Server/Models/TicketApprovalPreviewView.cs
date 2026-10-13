using AgentSmith.Server.Services.SpecDialog.Continuation;

namespace AgentSmith.Server.Models;

/// <summary>
/// 2026-10-13-9a59l: what approving the open proposal in a ticket's conversation would do — computed by
/// the same decision the approval applies. Effect is a <see cref="TicketContinuationEffects"/> value; each
/// spec is kept, recut, appended or dropped, with a note where one is owed. 2026-10-13-9a59m: Start is when
/// the attempt the approval asks for starts (<see cref="TicketStartArms"/>), for the caller's permissions.
/// </summary>
public sealed record TicketApprovalPreviewView(
    string Project,
    string TicketId,
    string Effect,
    string Reason,
    int Executed,
    int Unstarted,
    string? BasisRunId,
    IReadOnlyList<TicketContinuationSpec> Specs,
    TicketContinuationStart? Start = null);
