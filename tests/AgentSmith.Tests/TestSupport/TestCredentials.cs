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

    public static AgentSmithConfig Config(params (string Name, string Value)[] secrets) =>
        new() { Secrets = secrets.ToDictionary(s => s.Name, s => s.Value) };
}
