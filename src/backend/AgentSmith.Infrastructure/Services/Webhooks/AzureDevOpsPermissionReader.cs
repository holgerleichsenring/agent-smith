using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using AgentSmith.Infrastructure.Services.Providers.Source;
using Microsoft.VisualStudio.Services.Identity;

namespace AgentSmith.Infrastructure.Services.Webhooks;

/// <summary>
/// Two Azure DevOps calls with <c>AZURE_DEVOPS_TOKEN</c>: the Identities API turns the
/// identity id into its descriptor (PAT scope Identity: Read), and the Access Control Lists
/// query with <c>includeExtendedInfo=true</c> returns that descriptor's effective bits,
/// group grants included (PAT scope Security: Manage — Azure DevOps has no read-only one).
/// </summary>
public sealed class AzureDevOpsPermissionReader(
    SecretsProvider secrets,
    IAzDoClientFactory clients,
    IHttpClientFactory httpClientFactory) : IAzureDevOpsPermissionReader
{
    public async Task<AzureDevOpsEffectivePermission?> ReadAsync(
        string organizationUrl, Guid securityNamespaceId, IReadOnlyList<string> tokens,
        Guid identityId, CancellationToken cancellationToken)
    {
        var pat = secrets.GetRequired("AZURE_DEVOPS_TOKEN");
        var identities = await clients.CreateIdentityClient(organizationUrl, pat)
            .ReadIdentitiesAsync([identityId], QueryMembership.None, cancellationToken: cancellationToken);
        var descriptor = identities.FirstOrDefault()?.Descriptor
            ?? throw new InvalidOperationException($"Azure DevOps identity {identityId} was not found.");

        foreach (var token in tokens)
        {
            var url = $"{organizationUrl.TrimEnd('/')}/_apis/accesscontrollists/{securityNamespaceId}"
                      + $"?token={Uri.EscapeDataString(token)}"
                      + $"&descriptors={Uri.EscapeDataString($"{descriptor.IdentityType};{descriptor.Identifier}")}"
                      + "&includeExtendedInfo=true&recurse=false&api-version=7.1";
            var effective = await QueryAsync(url, pat, token, cancellationToken);
            if (effective is not null)
                return effective;
        }
        return null;
    }

    private async Task<AzureDevOpsEffectivePermission?> QueryAsync(
        string url, string pat, string token, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes($":{pat}")));
        using var response = await httpClientFactory.CreateClient().SendAsync(request, cancellationToken);
        await response.EnsureSuccessWithBodyAsync(cancellationToken);

        await using var body = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var doc = await JsonDocument.ParseAsync(body, cancellationToken: cancellationToken);
        foreach (var acl in doc.RootElement.GetProperty("value").EnumerateArray())
        {
            if (acl.GetProperty("token").GetString() != token) continue;
            foreach (var ace in acl.GetProperty("acesDictionary").EnumerateObject())
                if (ace.Value.TryGetProperty("extendedInfo", out var info))
                    return new AzureDevOpsEffectivePermission(Bits(info, "effectiveAllow"), Bits(info, "effectiveDeny"));
        }
        return null;
    }

    private static int Bits(JsonElement info, string name) =>
        info.TryGetProperty(name, out var value) ? value.GetInt32() : 0;
}
