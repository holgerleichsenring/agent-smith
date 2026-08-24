namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// p0349: the chicken-and-egg bootstrap the server reads from the file/env BEFORE
/// it can talk to the DB — the persistence connection (which cannot live in the DB
/// it describes) and the secret env-name references. Everything else loads from
/// DbConfigStore. The full agentsmith.yml shrinks from operating surface to this
/// bootstrap plus an import/export artifact.
/// <para>
/// p0503b: the auth block joins it for the same reason — the authority a token is
/// validated against decides who may read the store, so it cannot be read out of it.
/// Null means the installation declared no auth block at all, which is a different
/// state from one that is present and unusable.
/// </para>
/// </summary>
public sealed record BootstrapConfig(
    PersistenceConfig Persistence,
    IReadOnlyDictionary<string, string> Secrets,
    TokenAuthorityConfig? Auth = null)
{
    public static BootstrapConfig Default() => new(new PersistenceConfig(), new Dictionary<string, string>());
}
