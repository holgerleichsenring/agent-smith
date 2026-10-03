namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-10-02-3f06b: answers whether a cited path is a file, and how many lines it has. The
/// repository's architecture rule probes its own working tree; a design turn probes the
/// sandboxes it holds. The qualifier is passed as written, so a probe that can route by it may.
/// </summary>
public interface IEvidenceProbe
{
    Task<EvidenceProbeResult> ProbeAsync(string? qualifier, string path, CancellationToken ct);
}
