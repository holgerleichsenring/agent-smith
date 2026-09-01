namespace AgentSmith.Application.Models;

/// <summary>
/// p0379: how a bootstrap round handles the component's principles.md.
/// </summary>
public enum PrinciplesMode
{
    /// <summary>Pre-p0379 catalog (no core template): the skill writes the file via write_file.</summary>
    SkillWrites,

    /// <summary>The framework wrote the composed core+delta; the operator ratifies via the init PR.</summary>
    Transferred,

    /// <summary>A principles file already exists — ratified content is never overwritten on re-init.</summary>
    PreservedExisting,
}
