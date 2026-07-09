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
    string Build(PipelineContext pipeline);

    string BuildOutcomeFixNudge(string originalUserPrompt, string validationError);
}
