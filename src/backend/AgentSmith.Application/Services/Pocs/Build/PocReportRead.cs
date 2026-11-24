using AgentSmith.Contracts.Pocs;

namespace AgentSmith.Application.Services.Pocs.Build;

/// <summary>2026-11-23-933dd: what a run's scan report said — the image verdict (null when the run left no report), the
/// gate's exit status from gate.txt, or why the report could not be taken.</summary>
public sealed record PocReportRead(PocScanVerdict? Verdict, string? Gate, string? Problem)
{
    /// <summary>The revision is blocked only when the gate itself failed AND the rules find a blocking finding.</summary>
    public bool Blocks => Problem is null && Gate == PocBuildArtifacts.GateFailed && Verdict is { IsBlocked: true };
}
