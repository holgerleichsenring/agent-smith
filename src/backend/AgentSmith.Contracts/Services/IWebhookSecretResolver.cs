using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// p0506: the ONE answer to "what secret does this webhook platform require?". The
/// signature verifier, the tracker-auth preflight check and the connections panel all
/// read it here; before this it existed three times and two of the copies carried a
/// comment saying they mirrored the third.
/// </summary>
public interface IWebhookSecretResolver
{
    /// <summary>The platform's configured secrets, or null when the platform is not one we know.</summary>
    WebhookSecretSource? Resolve(string platform, AgentSmithConfig config);

    /// <summary>Every known platform, in the order the connections panel lists them.</summary>
    IReadOnlyList<WebhookSecretSource> ResolveAll(AgentSmithConfig config);
}
