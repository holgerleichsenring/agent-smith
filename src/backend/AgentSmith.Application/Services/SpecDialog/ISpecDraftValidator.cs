namespace AgentSmith.Application.Services.SpecDialog;

/// <summary>
/// p0315b: validates a design-partner reply's phase-spec draft (the fenced
/// ```yaml block, when present) against the phase-spec schema BEFORE the
/// reply is shown — an invalid draft re-prompts the master, never surfaces raw.
/// </summary>
public interface ISpecDraftValidator
{
    SpecDraftOutcome Validate(string reply);
}
