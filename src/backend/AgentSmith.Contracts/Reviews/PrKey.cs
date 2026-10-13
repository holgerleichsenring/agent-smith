using System.Security.Cryptography;
using System.Text;

namespace AgentSmith.Contracts.Reviews;

/// <summary>
/// 2026-10-13-9a59b: a pull request's key — the SHA-256 hex of its URL exactly as the run's
/// pull-request list stores it, trimmed. The one hash every reader and trigger joins on; it keeps
/// the unique index inside MySQL's indexed-string limit while the URL itself stays unindexed.
/// </summary>
public static class PrKey
{
    public static string Of(string url)
    {
        ArgumentNullException.ThrowIfNull(url);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(url.Trim())));
    }
}
