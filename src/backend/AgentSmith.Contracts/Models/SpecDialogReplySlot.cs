using AgentSmith.Contracts.Providers;

namespace AgentSmith.Contracts.Models;

/// <summary>
/// Mutable single-value holder the spec-dialog turn runner seeds into the
/// pipeline context (p0315b). The CollectSpecDialogReply step writes the
/// master's final reply into it; the turn runner reads it back after the
/// run — the pipeline context itself never leaves ExecutePipelineUseCase.
/// </summary>
public sealed class SpecDialogReplySlot
{
    public string? Reply { get; set; }

    /// <summary>
    /// p0315e: the typed terminal outcome the master's reply resolved to
    /// (answer / bug / spec / epic), written together with the reply.
    /// </summary>
    public OutcomeProposal? Outcome { get; set; }

    /// <summary>
    /// 2026-09-17-042ec: set when the reply is a notice rather than what the outcome says.
    /// </summary>
    public SpecDialogTurnKind? Kind { get; set; }

    /// <summary>The ticket steward led the turn, so its reply is an answer and a stored proposal is superseded.</summary>
    public bool Stewarded { get; set; }

    /// <summary>2026-11-04-96a0a: the conversation's name, as the master last gave it through name_conversation.</summary>
    public string? Subject { get; set; }

    /// <summary>2026-11-05-862ea: the work picture the master last stated through state_work_picture, or null.</summary>
    public WorkPicture? WorkPicture { get; set; }

    /// <summary>2026-11-10-d854a: the open questions the master last laid on its draft through state_open_questions.</summary>
    public IReadOnlyList<OpenQuestion> Questions { get; set; } = [];

    /// <summary>What the turn's master last forwarded and was answered, for the next turn to replay.</summary>
    public ForwardedThreadCapture Thread { get; } = new();
}
