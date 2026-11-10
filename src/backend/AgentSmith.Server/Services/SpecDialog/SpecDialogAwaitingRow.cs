using AgentSmith.Infrastructure.Persistence.Entities;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-11-10-ccd9: the proposal a session row holds as awaiting a decision, read the same way by every
/// surface. A row no build of ccd9 has touched yet holds it only in LatestProposalJson, with no id until a
/// read backfills one.
/// </summary>
public static class SpecDialogAwaitingRow
{
    public static string? Json(SpecDialogSession session) =>
        session.ProposalId is null && session.ReviewingProposalJson is null
            ? session.LatestProposalJson
            : session.AwaitingProposalJson;

    /// <summary>The id a decision on the awaiting proposal must carry; null while none awaits.</summary>
    public static string? Id(SpecDialogSession session) =>
        session.AwaitingProposalJson is null ? null : session.ProposalId;
}
