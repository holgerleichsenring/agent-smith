namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// One entry in the agentsmith.yml `registries:` list (p0191). Authenticates
/// the agent against a private package feed (NuGet / npm / pip / maven /
/// whatever) when the toolchain step inside a sandbox returns NU1301 /
/// EAUTH / 401. Token is the only sensitive field; matched on Host via the
/// dot-boundary subdomain rule in
/// <see cref="AgentSmith.Application.Services.Tools"/>.
/// </summary>
/// <param name="Host">DNS hostname of the feed, e.g. <c>pkgs.dev.azure.com</c>.</param>
/// <param name="Username">Username the feed expects; "any" for tokens that ignore it.</param>
/// <param name="Token">Resolved secret value (post env-var substitution).</param>
public sealed record RegistryConfig(string Host, string Username, string Token);
