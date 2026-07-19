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
public sealed class BootstrapConfigReader(IConfigStoreLocation location)
{
    public BootstrapConfig Read()
    {
        var path = location.ConfigPath;
        if (!File.Exists(path)) return BootstrapConfig.Default();
        try
        {
            var raw = RawConfigYaml.Deserialize(File.ReadAllText(path));
            return new BootstrapConfig(raw.Persistence, raw.Secrets);
        }
        catch (YamlDotNet.Core.YamlException)
        {
            // A malformed bootstrap file must not crash the DI graph build; the full
            // loader surfaces the parse error with detail on the next real load.
            return BootstrapConfig.Default();
        }
    }
}
