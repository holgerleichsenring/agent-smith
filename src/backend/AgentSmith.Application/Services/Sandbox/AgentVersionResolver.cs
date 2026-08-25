using AgentSmith.Contracts.Models.Configuration;
using Microsoft.Extensions.Options;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// 2026-08-25-0d01: derives the sandbox-agent tag from the release this server is, and
/// steps aside when an operator names one.
/// <para>
/// The image the tag belongs to is OUR carrier image, not anything a customer builds: its
/// name is a compiled-in constant and only the registry and the tag are configurable. So
/// the only skew a deployment can produce here is an operator pinning an old tag of our
/// own binary — which used to be MANDATORY, with an error message that told them to pick a
/// tag "matching the agent-smith release in use" and nothing on earth to enforce it.
/// Deriving it removes that whole class for everyone who does not opt out.
/// </para>
/// <para>
/// The derived tag is <see cref="BuildIdentity.Version"/>, which is version.txt as it stood
/// when the server image was built — always a tag the publish workflow released, because
/// that is the commit where the semver tags were applied. On a non-release trunk build the
/// server is newer than the agent at that tag, and that is fine: what has to match is the
/// wire protocol, not the release.
/// </para>
/// </summary>
public sealed class AgentVersionResolver(
    IOptions<SandboxGlobalConfig> globalConfig, BuildIdentity build) : IAgentVersionResolver
{
    public AgentVersionChoice Resolve(ResolvedProject projectConfig)
    {
        var pinned = FirstNonEmpty(projectConfig.Sandbox?.AgentVersion, globalConfig.Value.AgentVersion);
        if (!string.IsNullOrEmpty(pinned))
            return new AgentVersionChoice(pinned!, build.Version, IsPinned: true);

        var derived = build.Version?.Trim();
        if (string.IsNullOrEmpty(derived)) throw UndeducibleVersion();
        return new AgentVersionChoice(derived!, build.Version, IsPinned: false);
    }

    // The only remaining fail-loud: a server that cannot name its own release has nothing
    // to derive from. That is a hand-built binary, never a published image — the publish
    // workflow stamps the release into both halves — so the message names the way out.
    private static InvalidOperationException UndeducibleVersion() =>
        new("The sandbox agent version is normally derived from the release this server is, "
            + $"but this build carries no {BuildIdentity.VersionVariable} — it was not built by "
            + "the publish workflow. Set 'deployment.version' (or 'sandbox.agent_version', or "
            + "projects.<name>.sandbox.agent_version) in agentsmith.yml to name a published "
            + "tag explicitly.");

    private static string? FirstNonEmpty(string? a, string? b) =>
        !string.IsNullOrEmpty(a) ? a : b;
}
