using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Services.Pocs.Pipeline;

/// <summary>
/// 2026-11-23-933dd: the build pipeline definition for the host of a target's source repo — GitHub, Azure DevOps or
/// GitLab; any other host runs no pipeline and answers a problem. Every definition starts with <see cref="Marker"/>, so
/// an install recognises its own file and refuses to overwrite one it did not write.
/// </summary>
public sealed class PocPipelineTemplate(GitHubPocWorkflow github, AzurePocPipeline azure, GitLabPocCi gitlab)
{
    public const string Marker = "# agent-smith: poc-build";

    public (PocPipelineFile? File, string? Problem) Render(RepoType host, PocPipelineValues values)
    {
        ArgumentNullException.ThrowIfNull(values);
        try
        {
            return host switch
            {
                RepoType.GitHub => (github.Render(values), null),
                RepoType.AzureDevOps => (azure.Render(values), null),
                RepoType.GitLab => (gitlab.Render(values), null),
                _ => (null, $"a {host} repository's host runs no pipeline, so it cannot build PoC images"),
            };
        }
        catch (ArgumentException refused)
        {
            return (null, refused.Message);
        }
    }

    /// <summary>Whether <paramref name="content"/> is a definition an install wrote.</summary>
    public static bool IsOurs(string content) => content.StartsWith(Marker, StringComparison.Ordinal);
}
