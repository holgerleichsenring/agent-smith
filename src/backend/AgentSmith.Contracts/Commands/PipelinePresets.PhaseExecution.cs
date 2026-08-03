namespace AgentSmith.Contracts.Commands;

public static partial class PipelinePresets
{
    /// <summary>
    /// p0315d's preset name. p0393 collapsed its command list into <see cref="Code"/> —
    /// the two differed only in the steps the phase spec now carries — but the NAME
    /// survives as an alias, because it lives in operator trigger configuration
    /// (default_pipeline, label routing) that agent-smith does not own.
    /// </summary>
    public const string PhaseExecutionName = "phase-execution";
}
