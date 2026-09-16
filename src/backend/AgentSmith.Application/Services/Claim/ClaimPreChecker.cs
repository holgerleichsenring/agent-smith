using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Services.Claim;

/// <summary>
/// Config-level validation for an incoming ClaimRequest. Runs before any Redis or HTTP call,
/// so a misconfigured request never consumes rate limits.
/// </summary>
internal static class ClaimPreChecker
{
    public static ClaimRejectionReason? Check(ClaimRequest request, AgentSmithConfig config)
    {
        if (!config.Projects.TryGetValue(request.ProjectName, out var project))
            return ClaimRejectionReason.UnknownProject;

        if (PipelinePresets.TryResolve(request.PipelineName) is null)
            return ClaimRejectionReason.UnknownPipeline;

        if (!IsLabelTriggered(project, request.Platform, request.PipelineName))
            return ClaimRejectionReason.PipelineNotLabelTriggered;

        return null;
    }

    private static bool IsLabelTriggered(ResolvedProject project, string platform, string pipelineName)
    {
        var trigger = GetTrigger(project, platform);
        if (trigger is null) return false;

        // p0315d: phase-execution is hard-bound to the `phase` ticket label in
        // ProjectResolver, never declared in pipeline_from_label — the trigger
        // existing for the platform is the whole config-side requirement.
        if (string.Equals(pipelineName, PipelinePresets.PhaseExecutionName, StringComparison.OrdinalIgnoreCase))
            return true;

        // 2026-09-16-a4d7: an undeclared default still routes to the fallback, so the
        // reachability answer must read the same value PipelineResolver would return.
        var fallback = trigger.DefaultPipeline ?? PipelinePresets.UndeclaredFallbackPipeline;
        if (string.Equals(fallback, pipelineName, StringComparison.Ordinal))
            return true;

        return trigger.PipelineFromLabel is { } map
            && map.Values.Any(p => string.Equals(p, pipelineName, StringComparison.Ordinal));
    }

    private static WebhookTriggerConfig? GetTrigger(ResolvedProject project, string platform)
        => platform.ToLowerInvariant() switch
        {
            "github" => project.GithubTrigger,
            "gitlab" => project.GitlabTrigger,
            "azuredevops" => project.AzuredevopsTrigger,
            "jira" => project.JiraTrigger,
            _ => null
        };
}
