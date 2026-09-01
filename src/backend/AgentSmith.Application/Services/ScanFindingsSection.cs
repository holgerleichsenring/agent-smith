using System.Text;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services;

/// <summary>
/// The scanners' raw output as the scan master reads it, and the question of WHICH scan is
/// running.
/// <para>
/// 2026-09-01-0e80: the two scan paths differ in what the master can look at on its own. A
/// repository scan has the source, so the scanner list is held back until the master has
/// committed to what it found; an api scan's inputs ARE the scanner reports plus the
/// OpenAPI document, so there is nothing to look at first and its ordering is unchanged.
/// <see cref="HasScannerReports"/> is what tells the two apart.
/// </para>
/// </summary>
public static class ScanFindingsSection
{
    /// <summary>True when the run carries live-target scanner REPORTS — the api scan.</summary>
    public static bool HasScannerReports(PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        return pipeline.TryGet<NucleiResult>(ContextKeys.NucleiResult, out var nuclei) && nuclei is not null
            || pipeline.TryGet<SpectralResult>(ContextKeys.SpectralResult, out var spectral) && spectral is not null
            || pipeline.TryGet<ZapResult>(ContextKeys.ZapResult, out var zap) && zap is not null;
    }

    /// <summary>The observations the repository scanners appended, or null when there are none.</summary>
    public static IReadOnlyList<SkillObservation>? RepoFindings(PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        return pipeline.TryGet<List<SkillObservation>>(ContextKeys.SkillObservations, out var obs)
            && obs is { Count: > 0 } ? obs : null;
    }

    public static string Render(PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        if (HasScannerReports(pipeline))
        {
            pipeline.TryGet<NucleiResult>(ContextKeys.NucleiResult, out var nuclei);
            pipeline.TryGet<SpectralResult>(ContextKeys.SpectralResult, out var spectral);
            pipeline.TryGet<ZapResult>(ContextKeys.ZapResult, out var zap);
            return ApiScanFindingsCompressor.BuildSummary(nuclei, spectral, zap);
        }
        var findings = RepoFindings(pipeline);
        return findings is null
            ? "## Scanner Findings\n\n(no automated scanner findings)\n"
            : Format(findings);
    }

    private static string Format(IReadOnlyList<SkillObservation> observations)
    {
        var builder = new StringBuilder("## Scanner Findings\n\n");
        foreach (var observation in observations)
            builder.AppendLine(
                $"- [{observation.Severity}] {observation.Role} {observation.DisplayLocation} — {observation.Description}");
        return builder.ToString();
    }
}
