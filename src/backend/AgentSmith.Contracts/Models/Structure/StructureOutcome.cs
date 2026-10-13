namespace AgentSmith.Contracts.Models.Structure;

/// <summary>2026-10-06-8565b: what a measurement attempt came to.</summary>
public enum StructureOutcome
{
    /// <summary>The file was parsed and its units measured.</summary>
    Measured,

    /// <summary>No language claims the path, or it is test or generated code.</summary>
    NotMeasured,

    /// <summary>A language claims the path but its grammar could not be loaded.</summary>
    Unavailable,
}
