namespace AgentSmith.Application.Services.SpecDialog;

/// <summary>
/// p0315b: validates a design-partner reply's spec draft (the fenced
/// ```yaml block, when present) against the spec schema BEFORE the
/// reply is shown — an invalid draft re-prompts the master, never surfaces raw.
/// </summary>
public interface ISpecDraftValidator
{
    SpecDraftOutcome Validate(string reply);

    /// <summary>
    /// p0315e: validates a bare spec yaml document (no fence extraction)
    /// — the epic path validates the parent and each child draft with this.
    /// </summary>
    SpecDraftOutcome ValidateYaml(string yaml);
}
