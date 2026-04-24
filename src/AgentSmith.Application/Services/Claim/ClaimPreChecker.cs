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

    private static bool IsLabelTriggered(ProjectConfig project, string platform, string pipelineName)
    {
        var trigger = GetTrigger(project, platform);
        if (trigger is null) return false;

        if (string.Equals(trigger.DefaultPipeline, pipelineName, StringComparison.Ordinal))
            return true;

        return trigger.PipelineFromLabel.Values
            .Any(p => string.Equals(p, pipelineName, StringComparison.Ordinal));
    }

    private static WebhookTriggerConfig? GetTrigger(ProjectConfig project, string platform)
        => platform.ToLowerInvariant() switch
        {
            "github" => project.GithubTrigger,
            "gitlab" => project.GitlabTrigger,
            "azuredevops" => project.AzuredevopsTrigger,
            "jira" => project.JiraTrigger,
            _ => null
        };
}
