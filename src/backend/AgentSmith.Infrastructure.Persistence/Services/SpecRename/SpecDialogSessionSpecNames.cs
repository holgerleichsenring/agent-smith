using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;

namespace AgentSmith.Infrastructure.Persistence.Services.SpecRename;

/// <summary>2026-10-12-967ab: SpecDialogSessions — filing and transcript. 2026-11-17-a0b6d: the proposals are rows of
/// their own table, rewritten by <see cref="SpecDialogProposalSpecNames"/>.</summary>
public sealed class SpecDialogSessionSpecNames(SpecDialogJsonRewriter dialogs, RowPages pages) : IStoredSpecNameSurface
{
    public string Name => "SpecDialogSessions";

    public Task<int> RewriteAsync(AgentSmithDbContext db, CancellationToken ct) =>
        pages.RewriteAsync(db,
            db.SpecDialogSessions.Where(s => s.TranscriptJson.Contains("hase")
                || (s.LatestFilingJson != null && s.LatestFilingJson.Contains("hase"))),
            s => s.Id, Rewrite, ct);

    private bool Rewrite(SpecDialogSession row)
    {
        var filing = dialogs.RewriteFiling(row.LatestFilingJson);
        var transcript = dialogs.RewriteTranscript(row.TranscriptJson);
        row.LatestFilingJson = filing ?? row.LatestFilingJson;
        row.TranscriptJson = transcript ?? row.TranscriptJson;
        return filing is not null || transcript is not null;
    }
}
