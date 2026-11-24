using AgentSmith.Contracts.Providers;

namespace AgentSmith.Application.Services.Pocs.Build;

/// <summary>2026-11-23-933dd: the files the build pipeline leaves for agent smith — the scan job's compact report and the
/// gate's exit status (uploaded whether the job passes or fails), and the push job's pushed digest.</summary>
public static class PocBuildArtifacts
{
    public static PipelineArtifact Report { get; } = new("poc-scan-report", "scan", "report.json");

    public static PipelineArtifact Gate { get; } = new("poc-scan-report", "scan", "gate.txt");

    public static PipelineArtifact Digest { get; } = new("poc-image", "push", "digest.txt");

    /// <summary>The gate's exit status when it failed on a finding.</summary>
    public const string GateFailed = "1";

    /// <summary>The largest gate.txt or digest.txt read — a number or one digest line.</summary>
    public const long SmallFileBytes = 256; // not a limit: twice the longest line either file holds
}
