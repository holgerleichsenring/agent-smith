using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Core.Services.Configuration.Studio;

namespace AgentSmith.Infrastructure.Core.Services.Configuration;

/// <summary>
/// p0349: the SERVER's configuration loader — assembles the raw config from the DB
/// entity-document rows via the type&lt;-&gt;model map, overlays the bootstrap
/// persistence + secrets (which live in the file, not the DB they describe), then
/// runs the SAME raw-to-typed pipeline the file loader uses. An empty store yields
/// an empty-but-valid config, so the server boots UNCONFIGURED: the studio is
/// reachable to enter config while pipelines/pollers stay idle (no agents/trackers/
/// projects to act on) until a minimal-viable config exists.
/// </summary>
public sealed class DbConfigurationLoader(
    IConfigDocumentStore docStore,
    ConfigDocumentAssembler assembler,
    RawConfigMaterializer materializer,
    BootstrapConfigReader bootstrap) : IConfigurationLoader
{
    private const string Source = "db://config-entity";

    public ConfigFileReadFact? LastRead { get; private set; }

    public AgentSmithConfig LoadConfig(string configPath)
    {
        var raw = assembler.Assemble(docStore.LoadAll());
        ApplyBootstrap(raw);
        var config = materializer.Materialize(raw);
        LastRead = new ConfigFileReadFact(Source, DateTimeOffset.UtcNow);
        return config;
    }

    // Persistence must come from the bootstrap file (the DB connection cannot live
    // in the DB it describes); the bootstrap secret names win over any stored copy.
    private void ApplyBootstrap(RawAgentSmithConfig raw)
    {
        var boot = bootstrap.Read();
        raw.Persistence = boot.Persistence;
        foreach (var (name, value) in boot.Secrets)
            raw.Secrets[name] = value;
    }
}
