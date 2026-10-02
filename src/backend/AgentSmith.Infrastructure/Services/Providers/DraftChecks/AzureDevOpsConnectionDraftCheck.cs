using System.Runtime.CompilerServices;
using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Infrastructure.Services.Providers.DraftChecks;

/// <summary>
/// 2026-10-02-5f89b: an Azure DevOps connection draft — who the PAT is (_apis/connectionData,
/// which answers a refused PAT with a sign-in page rather than a 401), the team project, then the
/// project's git repositories (one unpaged list).
/// </summary>
public sealed class AzureDevOpsConnectionDraftCheck(IDraftCheckHttp http, AzureDevOpsDraftSteps shared)
    : IConnectionDraftCheck
{
    public RepoType Type => RepoType.AzureDevOps;

    public async IAsyncEnumerable<DraftCheckStep> RunAsync(
        ResolvedConnection connection, string token, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(connection.Organization) || string.IsNullOrWhiteSpace(connection.Project))
        {
            yield return DraftCheckStep.Fail(DraftCheckStep.Host, "The connection names no organization or project.");
            yield break;
        }
        var host = string.IsNullOrWhiteSpace(connection.Host) ? "https://dev.azure.com" : connection.Host.TrimEnd('/');
        var org = $"{host}/{Uri.EscapeDataString(connection.Organization)}";
        var project = Uri.EscapeDataString(connection.Project);
        await foreach (var step in shared.IdentityAndProjectAsync(org, connection.Project, token, cancellationToken))
            yield return step;

        var repos = await http.GetAsync(
            $"{org}/{project}/_apis/git/repositories?api-version=7.1", DraftCheckAuth.Basic(string.Empty, token),
            cancellationToken);
        yield return repos.CountStep(DraftCheckStep.Repos, "repositories visible",
            j => j.PageUnder("value") is { } page ? (page.Count, false) : null);
    }
}
