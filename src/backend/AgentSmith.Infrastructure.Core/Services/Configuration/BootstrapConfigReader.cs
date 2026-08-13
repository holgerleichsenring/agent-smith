using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Infrastructure.Core.Services.Configuration;

/// <summary>
/// p0349: reads ONLY the bootstrap slice (persistence + secret names) from the
/// agentsmith.yml at <see cref="IConfigStoreLocation.ConfigPath"/>. This is what
/// wires the DB connection before the rest of the config is loaded from that DB.
/// A missing/unparseable file yields defaults so the server can still boot
/// unconfigured (sqlite default) and the DI graph validates without a file present.
/// </summary>
public sealed class BootstrapConfigReader(IConfigStoreLocation location,
    RawConfigYaml rawConfigYaml,
    IStartupFindings? findings = null)
{
    private readonly IStartupFindings _findings = findings ?? new StartupFindings();

    public BootstrapConfig Read()
    {
        var path = location.ConfigPath;
        if (!File.Exists(path)) return BootstrapConfig.Default();
        try
        {
            var raw = rawConfigYaml.Deserialize(File.ReadAllText(path));
            return new BootstrapConfig(raw.Persistence, raw.Secrets);
        }
        catch (Exception ex)
        {
            // p0391b: the catch used to be YamlException only, so a file that EXISTS but
            // cannot be READ — wrong owner on a mounted ConfigMap, a directory where a file
            // was expected — threw out of the DbContext factory and killed the server on the
            // first scope. Any failure to read the bootstrap slice is now the same finding.
            _findings.Record(Unreadable(path, ex));
            return BootstrapConfig.Default();
        }
    }

    private static StartupFinding Unreadable(string path, Exception ex) => new(
        StartupSubsystems.ConfigFile,
        StartupFindingSeverity.Blocking,
        $"The bootstrap config at '{path}' could not be read, so the built-in defaults "
        + "(sqlite, no secret names) are in use and the configured database is NOT being "
        + $"used. Cause: {ex.Message}",
        Field: "CONFIG_PATH");
}
