using System.Runtime.CompilerServices;
using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Models;

namespace AgentSmith.Infrastructure.Services.Providers.DraftChecks;

/// <summary>
/// 2026-10-02-5f89b: a GitLab issues tracker draft — who the token is, then its project path.
/// The base is the tracker's own url, as <c>TrackerConnections.GitLab</c> builds it.
/// </summary>
public sealed class GitLabTrackerDraftCheck(IDraftCheckHttp http) : ITrackerDraftCheck
{
    public TrackerType Type => TrackerType.GitLab;

    public static string ApiOf(TrackerConnection tracker) =>
        $"{(string.IsNullOrWhiteSpace(tracker.Url) ? AgentDefaults.DefaultGitLabBaseUrl : tracker.Url.TrimEnd('/'))}/api/v4";

    public async IAsyncEnumerable<DraftCheckStep> RunAsync(
        TrackerConnection tracker, string token, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var auth = DraftCheckAuth.GitLab(token);
        var me = await http.GetAsync($"{ApiOf(tracker)}/user", auth, cancellationToken);
        yield return me.HostStep();
        yield return me.IdentityStep(j => j.Text("username"));

        if (string.IsNullOrWhiteSpace(tracker.Project))
        {
            yield return DraftCheckStep.Fail(DraftCheckStep.Scope, "The tracker names no project path.");
            yield break;
        }
        var project = await http.GetAsync(
            $"{ApiOf(tracker)}/projects/{Uri.EscapeDataString(tracker.Project)}", auth, cancellationToken);
        yield return project.ScopeStep($"Project '{tracker.Project}'");
    }
}
