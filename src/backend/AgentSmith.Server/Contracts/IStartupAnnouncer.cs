namespace AgentSmith.Server.Contracts;

/// <summary>
/// p0391a: logs what this server actually started with — the configuration summary and
/// the deprecation warnings. Separate from the probes because it reports the state that
/// IS there, not the dependencies that are missing.
/// </summary>
public interface IStartupAnnouncer
{
    void Announce();
}
