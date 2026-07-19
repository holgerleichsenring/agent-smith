namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// p0349: the chicken-and-egg bootstrap the server reads from the file/env BEFORE
/// it can talk to the DB — the persistence connection (which cannot live in the DB
/// it describes) and the secret env-name references. Everything else loads from
/// DbConfigStore. The full agentsmith.yml shrinks from operating surface to this
/// bootstrap plus an import/export artifact.
/// </summary>
public sealed record BootstrapConfig(
    PersistenceConfig Persistence,
    IReadOnlyDictionary<string, string> Secrets)
{
    public static BootstrapConfig Default() => new(new PersistenceConfig(), new Dictionary<string, string>());
}
