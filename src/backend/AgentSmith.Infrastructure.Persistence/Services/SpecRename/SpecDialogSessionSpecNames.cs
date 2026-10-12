using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;

namespace AgentSmith.Infrastructure.Persistence.Services.SpecRename;

/// <summary>2026-10-12-967ab: SpecDialogSessions — proposal, confirmed outcome, filing and transcript.</summary>
public sealed class SpecDialogSessionSpecNames(SpecDialogJsonRewriter dialogs, RowPages pages) : IStoredSpecNameSurface
{
    public string Name => "SpecDialogSessions";

    public Task<int> RewriteAsync(AgentSmithDbContext db, CancellationToken ct) =>
        pages.RewriteAsync(db,
            db.SpecDialogSessions.Where(s => s.TranscriptJson.Contains("hase")
                || (s.LatestProposalJson != null && s.LatestProposalJson.Contains("hase"))
                || (s.ConfirmedOutcomeJson != null && s.ConfirmedOutcomeJson.Contains("hase"))
                || (s.LatestFilingJson != null && s.LatestFilingJson.Contains("hase"))),
            s => s.Id, Rewrite, ct);

    private bool Rewrite(SpecDialogSession row)
    {
        var proposal = dialogs.RewriteOutcome(row.LatestProposalJson);
        var confirmed = dialogs.RewriteOutcome(row.ConfirmedOutcomeJson);
        var filing = dialogs.RewriteFiling(row.LatestFilingJson);
        var transcript = dialogs.RewriteTranscript(row.TranscriptJson);
        row.LatestProposalJson = proposal ?? row.LatestProposalJson;
        row.ConfirmedOutcomeJson = confirmed ?? row.ConfirmedOutcomeJson;
        row.LatestFilingJson = filing ?? row.LatestFilingJson;
        row.TranscriptJson = transcript ?? row.TranscriptJson;
        return proposal is not null || confirmed is not null || filing is not null || transcript is not null;
    }
}
