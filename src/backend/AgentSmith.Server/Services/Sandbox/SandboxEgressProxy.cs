namespace AgentSmith.Server.Services.Sandbox;

/// <summary>
/// 2026-10-25-c5a7g: the egress proxy an operator points sandboxes at (SANDBOX_EGRESS_PROXY).
/// Every process a step starts inherits the toolchain env, so git, curl and the package managers
/// go through it. Loopback and in-cluster names bypass it; Redis is not HTTP and needs no entry.
/// </summary>
public sealed record SandboxEgressProxy(string Url)
{
    public const string NoProxy = "localhost,127.0.0.1,.svc,.cluster.local";

    public static SandboxEgressProxy? From(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : new SandboxEgressProxy(value.Trim());

    public IReadOnlyList<KeyValuePair<string, string>> Env() =>
    [
        new("HTTP_PROXY", Url), new("HTTPS_PROXY", Url),
        new("http_proxy", Url), new("https_proxy", Url),
        new("NO_PROXY", NoProxy), new("no_proxy", NoProxy)
    ];
}
