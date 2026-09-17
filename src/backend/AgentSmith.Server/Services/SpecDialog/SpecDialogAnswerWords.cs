using AgentSmith.Contracts.Dialogue;
using AgentSmith.Server.Models;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-09-17-042el: the one approve/reject vocabulary. The confirmer reads a reply with it and
/// the answer admission records the same decision on the transcript, so the two cannot disagree
/// about what "ok" meant.
/// </summary>
public static class SpecDialogAnswerWords
{
    private static readonly string[] ApprovalAnswers =
        ["yes", "y", "approve", "approved", "ok", "confirm", "confirmed"];
    private static readonly string[] RejectionAnswers =
        ["no", "n", "reject", "rejected", "decline", "declined", "cancel", "discard", "drop"];

    /// <summary>The decision a reply states, trimmed and case-insensitive; null for anything else.</summary>
    public static SpecDialogDecision? DecisionIn(string reply)
    {
        var token = reply.Trim().ToLowerInvariant();
        if (ApprovalAnswers.Contains(token)) return SpecDialogDecision.Approved;
        if (RejectionAnswers.Contains(token)) return SpecDialogDecision.Rejected;
        return null;
    }

    /// <summary>
    /// The decision an answer records. Only an approval question is decided by a word: a design
    /// turn's own question is free text, where "yes" is what the person said.
    /// </summary>
    public static SpecDialogDecision? DecisionOn(DialogQuestion question, string reply) =>
        question.Type == QuestionType.Approval ? DecisionIn(reply) : null;
}
