namespace AgentSmith.Server.Models;

/// <summary>
/// 2026-10-13-9a59b: the decision a ticket shows for a pull request, strongest first — rejected,
/// changes requested, changes requested by someone the host does not trust, approved. An Azure
/// DevOps approval with suggestions reads approved.
/// </summary>
public static class TicketReviewDecisions
{
    public const string None = "none";
    public const string Approved = "approved";
    public const string ChangesRequested = "changes-requested";
    public const string ChangesRequestedUntrusted = "changes-requested-untrusted";
    public const string Rejected = "rejected";

    /// <summary>Why an untrusted request for changes started nothing.</summary>
    public const string UntrustedNote = "not a trusted reviewer, so no attempt was started";
}
