using System.Runtime.CompilerServices;
using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Models;

namespace AgentSmith.Infrastructure.Services.Providers.DraftChecks;

/// <summary>
/// 2026-10-02-5f89b: a GitHub issues tracker draft — who the token is, then the repository its
/// url names. 2026-10-25-9dbbb: both on the API of the host that url names, as the provider does.
/// </summary>
public sealed class GitHubTrackerDraftCheck(IDraftCheckHttp http) : ITrackerDraftCheck
{
    public TrackerType Type => TrackerType.GitHub;

    public async IAsyncEnumerable<DraftCheckStep> RunAsync(
        TrackerConnection tracker, string token, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var api = GitHubApiEndpoints.For(tracker.Url).RestRoot;
        var auth = DraftCheckAuth.GitHub(token);
        var me = await http.GetAsync($"{api}/user", auth, cancellationToken);
        yield return me.HostStep();
        yield return me.IdentityStep(j => j.Text("login"));

        if (!GitHubRepoAddress.TryParse(tracker.Url, out var address))
        {
            yield return DraftCheckStep.Fail(DraftCheckStep.Scope, "The tracker url names no owner/repository.");
            yield break;
        }
        yield return (await http.GetAsync($"{api}/repos/{address!.FullName}", auth, cancellationToken))
            .ScopeStep($"Repository '{address.FullName}'");
    }
}
