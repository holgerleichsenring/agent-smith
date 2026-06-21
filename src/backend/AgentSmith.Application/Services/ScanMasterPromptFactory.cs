using System.Text;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;

namespace AgentSmith.Application.Services;

/// <summary>
/// p0278: builds the review user prompt for a scan master. api-security inputs come
/// from the Nuclei/Spectral/ZAP results (+ the compressed OpenAPI spec); security-scan
/// inputs are the raw SkillObservations the scanner handlers already appended. Always
/// closes with the hard review framing so the master reviews instead of coding.
/// </summary>
public sealed class ScanMasterPromptFactory : IScanMasterPromptFactory
{
    public string Build(PipelineContext pipeline, Repository repository, IReadOnlyList<string> repoNames)
    {
        var repos = repoNames.Count > 1 ? $"**Repositories:** {string.Join(", ", repoNames)}\n" : string.Empty;
        return $"""
            You are running a SECURITY REVIEW, not a coding task. Do NOT modify any
            source, do NOT run a build, do NOT run tests. You have read-only tools.

            ## Working source
            **Path:** {repository.LocalPath}
            **Branch:** {repository.CurrentBranch}
            {repos}
            {BuildFindingsSection(pipeline)}
            {BuildSpecSection(pipeline)}
            Work your methodology over these scanner inputs and the source — read the
            implementing code to anchor each finding. When you are done, stop calling
            tools and output ONLY your final JSON observation array (an empty array
            `[]` if nothing survives your refutation step).
            """;
    }

    private static string BuildFindingsSection(PipelineContext pipeline)
    {
        pipeline.TryGet<NucleiResult>(ContextKeys.NucleiResult, out var nuclei);
        pipeline.TryGet<SpectralResult>(ContextKeys.SpectralResult, out var spectral);
        pipeline.TryGet<ZapResult>(ContextKeys.ZapResult, out var zap);
        if (nuclei is not null || spectral is not null || zap is not null)
            return ApiScanFindingsCompressor.BuildSummary(nuclei, spectral, zap);

        return pipeline.TryGet<List<SkillObservation>>(ContextKeys.SkillObservations, out var obs)
            && obs is { Count: > 0 }
            ? FormatObservations(obs)
            : "## Scanner Findings\n\n(no automated scanner findings)\n";
    }

    private static string FormatObservations(IReadOnlyList<SkillObservation> observations)
    {
        var sb = new StringBuilder("## Scanner Findings\n\n");
        foreach (var o in observations)
            sb.AppendLine(
                $"- [{o.Severity}] {o.Role} {o.DisplayLocation} — {o.Description}");
        return sb.ToString();
    }

    private static string BuildSpecSection(PipelineContext pipeline) =>
        pipeline.TryGet<string>(ContextKeys.SwaggerSpec, out var spec) && !string.IsNullOrWhiteSpace(spec)
            ? $"## OpenAPI spec (compressed)\n\n{spec}\n"
            : string.Empty;
}
