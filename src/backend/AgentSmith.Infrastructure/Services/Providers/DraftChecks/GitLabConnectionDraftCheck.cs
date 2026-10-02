using System.Runtime.CompilerServices;
using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Infrastructure.Services.Providers.DraftChecks;

/// <summary>
/// 2026-10-02-5f89b: a GitLab connection draft — who the token is (/api/v4/user), the group, then
/// one page of the projects discovery would list (same subgroup and sharing filters).
/// </summary>
public sealed class GitLabConnectionDraftCheck(IDraftCheckHttp http) : IConnectionDraftCheck
{
    public RepoType Type => RepoType.GitLab;

    public async IAsyncEnumerable<DraftCheckStep> RunAsync(
        ResolvedConnection connection, string token, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var api = $"{(string.IsNullOrWhiteSpace(connection.Host) ? "https://gitlab.com" : connection.Host.TrimEnd('/'))}/api/v4";
        var auth = DraftCheckAuth.GitLab(token);
        var me = await http.GetAsync($"{api}/user", auth, cancellationToken);
        yield return me.HostStep();
        yield return me.IdentityStep(j => j.Text("username"));

        if (string.IsNullOrWhiteSpace(connection.Group))
        {
            yield return DraftCheckStep.Fail(DraftCheckStep.Scope, "The connection names no group.");
            yield break;
        }
        var group = $"{api}/groups/{Uri.EscapeDataString(connection.Group)}";
        yield return (await http.GetAsync(group, auth, cancellationToken)).ScopeStep($"Group '{connection.Group}'");

        var projects = await http.GetAsync(
            $"{group}/projects?include_subgroups=true&with_shared=false&per_page={DraftCheckJson.PageSize}",
            auth, cancellationToken);
        yield return projects.CountStep(DraftCheckStep.Repos, "repositories visible", j => j.Page());
    }
}
