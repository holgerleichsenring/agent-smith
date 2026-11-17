using AgentSmith.Application.Models;
using AgentSmith.Application.Models.Sandbox;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Decisions;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// 2026-11-16-f3fbd: with <c>sandbox.allow_new_registries</c> on, a code or spec-derive run lets a refused
/// context image in by trusting its registry prefix — written through <see cref="ISandboxRegistryTrustWriter"/>
/// inside the run's own step, judged again against the boundary as it now stands, and recorded as a run
/// decision. pr-review and security-scan never widen it: they read branches a contributor wrote, and the
/// boundary is installation-wide.
/// </summary>
public sealed class RegistryTrustAdmission(
    ISandboxSettingsReader settings, ISandboxRegistryTrustWriter writer, ContextImageBoundary boundary,
    IDecisionLogger decisions, ILogger<RegistryTrustAdmission> logger)
{
    private static readonly IReadOnlyList<string> WideningPipelines =
        [PipelinePresets.CodeName, PipelinePresets.SpecDeriveName];

    /// <summary>Null once the image is admitted; otherwise the refusal that still stands.</summary>
    public async Task<ContextImageRefusal?> AdmitAsync(
        PipelineContext pipeline, string context, ContextImageRefusal refusal, CancellationToken cancellationToken)
    {
        if (!Widens(pipeline)) return refusal;
        if (!pipeline.TryGet<string>(ContextKeys.RunId, out var runId) || string.IsNullOrWhiteSpace(runId))
            return refusal;
        var prefix = RegistryTrustBoundary.PrefixOf(refusal.Image);
        var write = await writer.TrustAsync(prefix, runId, cancellationToken);
        if (!write.Trusted) return refusal with { Lever = write.Message + "; " + refusal.Lever };
        var still = boundary.Judge(refusal.Image);
        if (still is not null) return still;
        await decisions.LogAsync(null, DecisionCategory.Tooling,
            $"trusted {prefix} for {refusal.Image} (context {context})", cancellationToken);
        return null;
    }

    private bool Widens(PipelineContext pipeline)
    {
        var name = pipeline.TryGet<string>(ContextKeys.PipelineName, out var n) ? n : null;
        if (!WideningPipelines.Contains(name, StringComparer.OrdinalIgnoreCase)) return false;
        try
        {
            return settings.Current().AllowNewRegistries;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "The sandbox settings could not be read — no registry is added");
            return false;
        }
    }
}
