using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-09-22-2d11b: the credential a sandbox-side git step talks to a remote with — one
/// definition, shared by the checkout ladder and the held tree's refresh. The helper writes
/// the token to stdout rather than to disk, so nothing survives the step that used it.
/// </summary>
internal static class GitStepCredentials
{
    public const string Helper =
        "credential.helper=!f() { echo \"username=x-access-token\"; echo \"password=$GIT_TOKEN\"; }; f";

    /// <summary>Null when the platform has no token configured — git then prompts nobody.</summary>
    public static IReadOnlyDictionary<string, string>? TokenEnv(RepoConnection config)
    {
        var token = GitTokenResolver.Resolve(config.Type);
        return token is null
            ? null
            : new Dictionary<string, string> { ["GIT_TOKEN"] = token };
    }
}
