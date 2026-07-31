using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Infrastructure.Core.Services.Configuration.Studio;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Infrastructure.Core.Services.Configuration;

/// <summary>
/// p0349: the SERVER's configuration loader — assembles the raw config from the DB
/// entity-document rows via the type&lt;-&gt;model map, overlays the bootstrap
/// persistence + secrets (which live in the file, not the DB they describe), then
/// runs the SAME raw-to-typed pipeline the file loader uses. An empty store yields
/// an empty-but-valid config, so the server boots UNCONFIGURED: the studio is
/// reachable to enter config while pipelines/pollers stay idle.
///
/// p0391a: an UNREADABLE store yields the same empty config plus a blocking finding.
/// Every startup path funnels through here — Program, five leader loops and the
/// AgentSmithConfig factory — so a throw escaping this method is a dead process that
/// cannot report why. The empty config keeps the pipelines idle; the finding says why.
/// </summary>
public sealed class DbConfigurationLoader(
    IConfigDocumentStore docStore,
    ConfigDocumentAssembler assembler,
    RawConfigMaterializer materializer,
    BootstrapConfigReader bootstrap,
    ILogger<DbConfigurationLoader>? logger = null,
    IStartupFindings? findings = null) : IConfigurationLoader
{
    private const string Source = "db://config-entity";

    private readonly ILogger _logger = logger ?? NullLogger<DbConfigurationLoader>.Instance;
    private readonly IStartupFindings _findings = findings ?? new StartupFindings();

    public ConfigFileReadFact? LastRead { get; private set; }

    public AgentSmithConfig LoadConfig(string configPath)
    {
        try
        {
            var config = Assemble();
            _findings.Clear(StartupSubsystems.Database);
            LastRead = new ConfigFileReadFact(Source, DateTimeOffset.UtcNow);
            return config;
        }
        catch (ConfigurationException ex)
        {
            return Degraded(ex, StartupSubsystems.Configuration,
                $"The stored configuration is not usable: {ex.Message}");
        }
        catch (Exception ex)
        {
            return Degraded(ex, StartupSubsystems.Database,
                "The configuration store could not be read, so the server is running with an "
                + $"empty configuration — no projects, trackers or pollers. Cause: {ex.Message}");
        }
    }

    private AgentSmithConfig Assemble()
    {
        var raw = assembler.Assemble(docStore.LoadAll());
        ApplyBootstrap(raw);
        return materializer.Materialize(raw);
    }

    private AgentSmithConfig Degraded(Exception ex, string subsystem, string reason)
    {
        _logger.LogError(ex, "Configuration load failed ({Subsystem}) — continuing empty", subsystem);
        _findings.Record(new StartupFinding(
            subsystem, StartupFindingSeverity.Blocking, reason, Field: "configuration"));
        return AgentSmithConfig.Empty();
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
