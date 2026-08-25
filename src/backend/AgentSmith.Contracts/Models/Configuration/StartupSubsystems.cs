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

    /// <summary>2026-08-25-8c97: which build a half of the product came from.</summary>
    public const string Build = "build";

    /// <summary>p0503b: the token authority a caller is validated against.</summary>
    public const string Auth = "auth";

    /// <summary>2026-08-25-0d01: which sandbox-agent build this installation spawns, and
    /// whether the wire it answers on is one this server reads.</summary>
    public const string SandboxAgent = "sandbox-agent";
}
