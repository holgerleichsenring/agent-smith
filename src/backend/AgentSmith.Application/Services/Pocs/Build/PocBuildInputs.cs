using AgentSmith.Application.Services.Pocs.Pipeline;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;

namespace AgentSmith.Application.Services.Pocs.Build;

/// <summary>
/// 2026-11-23-933dd: the start order of a build — the pipeline named as its host names it (GitHub the workflow file,
/// Azure DevOps build.pipeline, GitLab its own .gitlab-ci.yml), on the default branch, with the inputs under the host's
/// keys: workflow inputs and template parameters poc, folder, sha, trivy_ignore, ignore_unfixed, secret_rules; GitLab
/// variables POC_NAME, POC_FOLDER, POC_SHA, POC_TRIVY_IGNORE, POC_IGNORE_UNFIXED, POC_SECRET_RULES. An empty list is
/// sent as 'none', because Azure DevOps refuses an empty template parameter.
/// </summary>
public sealed class PocBuildInputs
{
    public const string Empty = "none";

    public PipelineRunOrder Order(PocBuildJob job, IReadOnlySet<string> ignore, IReadOnlyList<string> secretRules)
    {
        ArgumentNullException.ThrowIfNull(job);
        var gitLab = job.Repo.Type == RepoType.GitLab;
        var values = new (string Input, string Variable, string Value)[]
        {
            ("poc", "POC_NAME", job.Subject.PocName), ("folder", "POC_FOLDER", job.Subject.Folder), ("sha", "POC_SHA", job.Sha),
            ("trivy_ignore", "POC_TRIVY_IGNORE", List(ignore.Order(StringComparer.Ordinal))),
            ("ignore_unfixed", "POC_IGNORE_UNFIXED", job.Scan.UnfixedCriticalBlock ? "false" : "true"),
            ("secret_rules", "POC_SECRET_RULES", List(secretRules)),
        };
        var inputs = values.ToDictionary(v => gitLab ? v.Variable : v.Input, v => v.Value, StringComparer.Ordinal);
        var pipeline = job.Repo.Type switch
        {
            RepoType.GitHub => System.IO.Path.GetFileName(GitHubPocWorkflow.Path),
            RepoType.AzureDevOps => job.Build.Pipeline,
            _ => null,
        };
        return new PipelineRunOrder(pipeline, null, inputs);
    }

    private static string List(IEnumerable<string> ids)
    {
        var joined = string.Join(',', ids);
        return joined.Length == 0 ? Empty : joined;
    }
}
