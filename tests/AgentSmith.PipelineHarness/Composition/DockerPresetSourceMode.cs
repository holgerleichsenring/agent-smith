namespace AgentSmith.PipelineHarness.Composition;

/// <summary>
/// p0199f: discriminates the two docker-tier api-security-scan paths.
/// <see cref="Passive"/> is the production-shape default — no source
/// checkout, BootstrapGate skips on <c>source_available=false</c>, scanners
/// probe a live HTTP target. <see cref="Source"/> is the opt-in branch
/// that points <c>SourcePath</c> at a checked-out repo so the source-aware
/// scan variant exercises BootstrapCheck against real .agentsmith content.
/// All other presets default to <see cref="Source"/> (they target code).
/// </summary>
internal enum DockerPresetSourceMode
{
    Source,
    Passive,
}
