using System.Net.Http.Headers;
using System.Text;
using AgentSmith.Contracts.Services;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.OAuth;

namespace AgentSmith.Infrastructure.Services.Providers;

/// <summary>
/// 2026-11-25-d0e2: how every Azure DevOps client — Azure Repos, Boards, PR diff, permission reads — sends its token. An Entra
/// access token (<see cref="AzureDevOpsBearer"/>) goes as Bearer, the client library's <see cref="VssOAuthAccessTokenCredential"/>,
/// as Microsoft documents for service principals; a personal access token goes as Basic with an empty user.
/// </summary>
public static class AzureDevOpsAuth
{
    public static AuthenticationHeaderValue Header(string token) => AzureDevOpsBearer.Is(token)
        ? new AuthenticationHeaderValue("Bearer", token)
        : new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes($":{token}")));

    public static VssCredentials Credential(string token) => AzureDevOpsBearer.Is(token)
        ? new VssOAuthAccessTokenCredential(token)
        : new VssBasicCredential(string.Empty, token);
}
