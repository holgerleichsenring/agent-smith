using System.Runtime.CompilerServices;
using System.Text.Json;
using AgentSmith.Contracts.Models.ConfigStudio;

namespace AgentSmith.Infrastructure.Services.Providers.DraftChecks;

/// <summary>
/// 2026-10-02-5f89b: the host, identity and project steps an Azure DevOps connection and an Azure
/// DevOps tracker share — one organization URL, one PAT, one team project.
/// </summary>
public sealed class AzureDevOpsDraftSteps(IDraftCheckHttp http)
{
    public async IAsyncEnumerable<DraftCheckStep> IdentityAndProjectAsync(
        string organizationUrl, string project, string token,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var auth = DraftCheckAuth.Basic(string.Empty, token);
        var me = await http.GetAsync($"{organizationUrl}/_apis/connectionData", auth, cancellationToken);
        yield return me.HostStep();
        yield return me.IdentityStep(DisplayName);

        var scope = await http.GetAsync(
            $"{organizationUrl}/_apis/projects/{Uri.EscapeDataString(project)}?api-version=7.1", auth, cancellationToken);
        yield return scope.ScopeStep($"Project '{project}'");
    }

    private static string? DisplayName(JsonElement json) =>
        json.TryGetProperty("authenticatedUser", out var user) ? user.Text("providerDisplayName") : null;
}
