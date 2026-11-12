using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.SpecDialog.Writes;

/// <summary>2026-11-12-cd4ca: a ticket comment written into a draft.</summary>
public static class CommentDraftWrite
{
    public static DraftWrite Write(ProposalDraft draft, string? comment)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if (DraftKind.Conflict(draft, DraftKind.Comment) is { } conflict) return DraftWrite.Refused(conflict);
        var text = (comment ?? string.Empty).Trim();
        if (text.Length == 0) return DraftWrite.Refused("the comment is empty");
        return DraftWrite.To(draft with { Comment = text });
    }
}
