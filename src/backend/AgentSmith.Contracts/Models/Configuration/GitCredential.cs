namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// 2026-10-02-5f89g: what a clone, fetch or push authenticates with — the token behind the
/// repo's own auth secret, or nothing for a local working copy. The git credential helper
/// echoes it as <c>$GIT_TOKEN</c>; <see cref="None"/> sets no such variable.
/// <para>2026-11-25-d0e2: a <see cref="Bearer"/> credential — an Entra access token for Azure Repos — is sent as the header
/// git documents for it, <c>http.extraheader: AUTHORIZATION: bearer …</c>, read through <c>--config-env</c> from
/// <see cref="BearerHeaderVariable"/> so the token stays out of the command line and no <c>GIT_CONFIG_*</c> entry the sandbox
/// already sets (safe.directory) is replaced. <see cref="Scope"/> (the remote's scheme and host) keeps the header to that host,
/// never a submodule's or a redirect's. <c>--config-env</c> needs git 2.31 or later.</para>
/// </summary>
public sealed record GitCredential(string? Token, bool Bearer = false, string? Scope = null)
{
    public const string BearerHeaderVariable = "AGENTSMITH_GIT_AUTH_HEADER";

    public static GitCredential None { get; } = new((string?)null);

    public bool HasToken => !string.IsNullOrEmpty(Token);

    /// <summary>The step env that carries the token; null for <see cref="None"/>.</summary>
    public IReadOnlyDictionary<string, string>? Env()
    {
        if (!HasToken) return null;
        var env = new Dictionary<string, string> { ["GIT_TOKEN"] = Token! };
        if (Bearer) env[BearerHeaderVariable] = $"AUTHORIZATION: bearer {Token}";
        return env;
    }

    /// <summary>The git options before the subcommand: the credential helper, and the bearer header for a bearer token.</summary>
    public string[] Args(string credentialHelper) => Bearer && HasToken
        ? [$"--config-env=http.{(Scope is null ? string.Empty : Scope + ".")}extraheader={BearerHeaderVariable}", "-c", credentialHelper]
        : ["-c", credentialHelper];
}
