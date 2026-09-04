namespace AgentSmith.Contracts.Constants;

/// <summary>
/// 2026-09-04-102b: the environment variables that name the system-of-record database.
/// They live here because three projects read them and only this one is shared by all
/// three — the configuration bootstrap, the file loader, and the design-time factories
/// <c>dotnet ef</c> builds a migration against.
/// <para>
/// The two are read TOGETHER. The provider decides how the connection string is parsed,
/// so a connection string without its provider is a string in an unknown grammar.
/// </para>
/// </summary>
public static class PersistenceEnvKeys
{
    public const string Provider = "AGENTSMITH_PERSISTENCE_PROVIDER";
    public const string Connection = "AGENTSMITH_PERSISTENCE_CONNECTION";
}
