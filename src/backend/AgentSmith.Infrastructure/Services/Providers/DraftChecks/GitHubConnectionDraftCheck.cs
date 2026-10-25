using System.Runtime.CompilerServices;
using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Models;

namespace AgentSmith.Infrastructure.Services.Providers.DraftChecks;

/// <summary>
/// 2026-10-02-5f89b: a GitHub connection draft — who the token is (/user), the owner as an org or
/// else a user (discovery's own fallback), then one page of that owner's repositories.
/// 2026-10-25-9dbbb: the connection's host is the web root; the API is derived from it.
/// </summary>
public sealed class GitHubConnectionDraftCheck(IDraftCheckHttp http) : IConnectionDraftCheck
{
    public RepoType Type => RepoType.GitHub;

    public async IAsyncEnumerable<DraftCheckStep> RunAsync(
        ResolvedConnection connection, string token, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var api = GitHubApiEndpoints.For(connection.Host).RestRoot;
        var auth = DraftCheckAuth.GitHub(token);
        var me = await http.GetAsync($"{api}/user", auth, cancellationToken);
        yield return me.HostStep();
        yield return me.IdentityStep(j => j.Text("login"));

        if (string.IsNullOrWhiteSpace(connection.Owner))
        {
            yield return DraftCheckStep.Fail(DraftCheckStep.Scope, "The connection names no owner.");
            yield break;
        }
        var owner = Uri.EscapeDataString(connection.Owner);
        var kind = "orgs";
        var scope = await http.GetAsync($"{api}/orgs/{owner}", auth, cancellationToken);
        if (scope.Status == 404)
        {
            kind = "users";
            scope = await http.GetAsync($"{api}/users/{owner}", auth, cancellationToken);
        }
        yield return scope.ScopeStep($"Owner '{connection.Owner}'");

        var repos = await http.GetAsync(
            $"{api}/{kind}/{owner}/repos?per_page={DraftCheckJson.PageSize}", auth, cancellationToken);
        yield return repos.CountStep(DraftCheckStep.Repos, "repositories visible", j => j.Page());
    }
}
