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
    /// (answer / bug / phase / epic), written together with the reply.
    /// </summary>
    public OutcomeProposal? Outcome { get; set; }
}
