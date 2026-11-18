using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Tests.TestSupport;

/// <summary>
/// 2026-10-02-5f89a: the real <see cref="CredentialResolver"/> over a secrets catalog given
/// inline — what a test configures instead of setting a process-wide token variable.
/// </summary>
public static class TestCredentials
{
    public static ICredentialResolver With(params (string Name, string Value)[] secrets) =>
        new CredentialResolver(new LoadedSecretValues(Config(secrets)));

    /// <summary>2026-11-18-9ae8: the resolver over an empty catalog — the process environment and nothing else.</summary>
    public static ICredentialResolver EnvironmentOnly => With();

    /// <summary>2026-11-18-9ae8: the resolver over the given catalog and the given environment, nothing of the process.</summary>
    public static ICredentialResolver Isolated(
        Dictionary<string, string> environment, params (string Name, string Value)[] secrets) =>
        new CredentialResolver(new LoadedSecretValues(Config(secrets)), environment.GetValueOrDefault);

    public static AgentSmithConfig Config(params (string Name, string Value)[] secrets) =>
        new() { Secrets = secrets.ToDictionary(s => s.Name, s => s.Value) };
}
