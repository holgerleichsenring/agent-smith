namespace AgentSmith.Tests.TestSupport;

/// <summary>
/// Test classes that mutate process-global environment variables share this
/// collection so xUnit serializes their execution. Without it, parallel runs
/// can leak env vars across tests that read the same variables (e.g. SecretsProvider-based
/// factory tests asserting "env not set → ConfigurationException").
/// </summary>
[CollectionDefinition(Name)]
public sealed class EnvVarCollection
{
    public const string Name = "EnvVarMutating";
}
