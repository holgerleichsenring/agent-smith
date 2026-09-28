using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Core.Services;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using FluentAssertions;

namespace AgentSmith.Tests.ConfigStudio;

/// <summary>
/// trace joins the stored settings. A server reads its configuration from the store, and the
/// store had no trace document, so tracing could be switched on only by environment; an
/// import dropped the block and an export emitted it.
/// </summary>
[Collection(nameof(TraceEnvironment))]
public sealed class TraceStoredSettingTests : IDisposable
{
    private readonly DbConfigTestHarness _h = new();
    private readonly string _bootstrapPath =
        Path.Combine(Path.GetTempPath(), $"agentsmith-trace-{Guid.NewGuid():N}.yml");
    private readonly string? _savedTrace = Environment.GetEnvironmentVariable(TraceSwitch.EnvironmentVariable);

    public TraceStoredSettingTests()
    {
        File.WriteAllText(_bootstrapPath, "persistence:\n  provider: sqlite\n  connection_string: 'Data Source=x.db'\n");
        Environment.SetEnvironmentVariable(TraceSwitch.EnvironmentVariable, null);
    }

    [Fact]
    public void DbConfigurationLoader_StoredTraceEnabled_YieldsTraceOn()
    {
        _h.Import("trace:\n  enabled: true\n");

        Loader().LoadConfig("ignored").Trace.Enabled.Should().BeTrue();
    }

    [Fact]
    public void ConfigImport_TraceBlock_RoundTripsThroughStore()
    {
        _h.Import("trace:\n  enabled: true\n");

        _h.Store.ExportYaml().Should().Contain("trace:").And.Contain("enabled: true");
    }

    [Fact]
    public void TraceSwitch_EnvOff_WinsOverStoredOn()
    {
        _h.Import("trace:\n  enabled: true\n");
        var config = Loader().LoadConfig("ignored");
        Environment.SetEnvironmentVariable(TraceSwitch.EnvironmentVariable, "0");

        new TraceSwitch(config).IsOn.Should().BeFalse("the environment wins over the store");
    }

    private DbConfigurationLoader Loader() =>
        new(_h.DocStore, _h.Assembler,
            new RawConfigMaterializer(
                new ProjectConfigNormalizer(), new EffectiveTriggerBuilder(), new DeploymentDefaultsApplier(),
                new ConfigCatalogResolver(), new AgentSmithPaths()),
            new BootstrapConfigReader(
                new FixedLocation(_bootstrapPath), new RawConfigYaml(), new AuthEnvironmentOverlay(),
                new PersistenceEnvironmentOverlay()));

    private sealed record FixedLocation(string ConfigPath) : IConfigStoreLocation;

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(TraceSwitch.EnvironmentVariable, _savedTrace);
        _h.Dispose();
        if (File.Exists(_bootstrapPath)) File.Delete(_bootstrapPath);
    }
}
