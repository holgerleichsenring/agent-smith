using AgentSmith.Contracts.Commands;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// p0315b: builds the design-partner master's user prompt for one spec-dialog
/// turn (the running transcript + the reply contract) and the focused
/// re-prompt used when the reply's terminal outcome (phase draft, bug or
/// epic block — p0315e) fails validation.
/// </summary>
public interface ISpecDialogPromptFactory
{
    /// <summary>
    /// 2026-09-20-3af8: the two NUMBERS, not a rendered sentence — how many images the
    /// conversation holds and how many ride this message. The vision flag and the per-turn
    /// ceiling are applied before the call; the wording belongs beside the reply contract.
    /// </summary>
    string Build(PipelineContext pipeline, int imagesExisting, int imagesCarried);

    string BuildOutcomeFixNudge(string originalUserPrompt, string validationError);

    /// <summary>
    /// 2026-09-17-042ec: the re-prompt for a turn that proposed before the operator replied
    /// to a discussion — answer instead, with no draft.
    /// </summary>
    string BuildProposalRefusalNudge(string originalUserPrompt);
}
