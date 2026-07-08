using AgentSmith.Contracts.Commands;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// p0315b: builds the design-partner master's user prompt for one spec-dialog
/// turn (the running transcript + the reply contract) and the focused
/// re-prompt used when a drafted phase spec fails schema validation.
/// </summary>
public interface ISpecDialogPromptFactory
{
    string Build(PipelineContext pipeline);

    string BuildDraftFixNudge(string originalUserPrompt, string validationError);
}
