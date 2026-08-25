using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using StackExchange.Redis;

namespace AgentSmith.Server.Services.Sandbox;

/// <summary>
/// p0465: derives the <see cref="SandboxOwnerIdentity"/> from the Redis endpoint the
/// sandbox is handed (<c>--redis-url</c>/<c>REDIS_URL</c>) — the address through which
/// the agent reaches the active-run set, i.e. the store its owner judges liveness
/// against. Two servers on one Docker daemon that share that store are one owner and
/// clean up after each other; two that do not share it never see each other's
/// containers. The DB lease is not folded in: a server without one runs no reaper at
/// all (see <see cref="SandboxReaperActivation"/>), so it cannot mis-judge anything.
/// An operator override wins over the derivation.
/// </summary>
public sealed partial class SandboxOwnerIdentityResolver
{
    public const string OverrideEnvVar = "SANDBOX_OWNER_ID";
    private const int MaxLabelLength = 63;

    public SandboxOwnerIdentity Resolve(string redisUrl) =>
        Resolve(redisUrl, Environment.GetEnvironmentVariable(OverrideEnvVar));

    public SandboxOwnerIdentity Resolve(string redisUrl, string? operatorOverride) =>
        new(string.IsNullOrWhiteSpace(operatorOverride)
            ? "store-" + Fingerprint(Canonicalize(redisUrl))
            : AsLabelValue(operatorOverride.Trim()));

    // An override that is already a legal label value is used verbatim, so the
    // operator sees the name they chose; anything else is folded to a stable
    // fingerprint rather than rejected at startup or silently truncated.
    private static string AsLabelValue(string candidate) =>
        candidate.Length <= MaxLabelLength && LabelValueRegex().IsMatch(candidate)
            ? candidate
            : "owner-" + Fingerprint(candidate);

    // Endpoints + database index, order-independent: 'redis:6379' and
    // 'redis:6379,abortConnect=false' are the same store and must fingerprint alike.
    private static string Canonicalize(string redisUrl)
    {
        try
        {
            var options = ConfigurationOptions.Parse(redisUrl);
            var endpoints = options.EndPoints.Select(e => e.ToString() ?? string.Empty).Order(StringComparer.Ordinal);
            return string.Join(',', endpoints) + "/" + (options.DefaultDatabase ?? 0);
        }
        catch (Exception)
        {
            // Unparseable configuration is still a stable string; fingerprinting it
            // keeps two servers with the identical setting on the same identity.
            return redisUrl.Trim().ToLowerInvariant();
        }
    }

    private static string Fingerprint(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)))[..16];

    [GeneratedRegex("^[A-Za-z0-9]([A-Za-z0-9._-]*[A-Za-z0-9])?$")]
    private static partial Regex LabelValueRegex();
}
