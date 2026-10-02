using System.Security.Cryptography;
using System.Text;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// 2026-10-02-5f89a: the key a pooled Azure DevOps connection is cached under — the organization
/// URL and a SHA-256 of the token. The org alone let a second connection to one organization, or
/// a re-pointed secret, keep the first token for the cache's lifetime; the hash keeps the token
/// itself out of a dictionary key.
/// </summary>
public static class AzureDevOpsConnectionKey
{
    public static string For(string organizationUrl, string personalAccessToken) =>
        $"{organizationUrl}|{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(personalAccessToken)))}";
}
