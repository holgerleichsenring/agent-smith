namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// p0391a: the subsystem names a <see cref="StartupFinding"/> can name. A finding
/// clears per subsystem on the next successful probe, so producers and clearers
/// must agree on the exact string — hence constants rather than literals.
/// </summary>
public static class StartupSubsystems
{
    public const string Configuration = "configuration";
    public const string ConfigFile = "config-file";
    public const string Database = "database";
    public const string Redis = "redis";
    public const string Spawner = "spawner";
}
