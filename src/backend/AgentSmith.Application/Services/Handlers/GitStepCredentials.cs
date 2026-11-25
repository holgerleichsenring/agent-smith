using AgentSmith.Contracts.Models.Configuration;

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

    /// <summary>
    /// 2026-10-02-5f89g: the step env for the repo's own credential; null for
    /// <see cref="GitCredential.None"/>, a local working copy that talks to no remote host.
    /// </summary>
    public static IReadOnlyDictionary<string, string>? TokenEnv(GitCredential credential) => credential.Env(); // 2026-11-25-d0e2

    /// <summary>2026-11-25-d0e2: the git options before a remote-facing subcommand, the bearer header included.</summary>
    public static string[] Args(GitCredential credential) => credential.Args(Helper);
}
