using AgentSmith.Application.Services.Events;
using AgentSmith.Contracts.Constants;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Core.Services;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using AgentSmith.Infrastructure.Core.Services.Configuration.Studio;
using FluentAssertions;

namespace AgentSmith.Tests.Configuration;

/// <summary>
/// 2026-09-04-102b: where the system-of-record database comes from. A cluster injects a
/// database credential as an environment variable from a Secret, not as a line in a shared
/// ConfigMap — so the environment replaces the file's persistence block.
/// <para>
/// The binding test is <see cref="BothVariables_ReachTheServerAndTheMigratingCli_Identically"/>:
/// the server reads through <see cref="BootstrapConfigReader"/> and the CLI that runs
/// `database migrate` as the init container reads through <see cref="YamlConfigurationLoader"/>.
/// An overlay wired into only one of them would leave the init container migrating a database
/// the server never opens, and a separate test per reader would be green while it happened.
/// </para>
/// </summary>
[Collection(TestSupport.EnvVarCollection.Name)]
public sealed class PersistenceEnvironmentTests : IDisposable
{
    private const string FileConnection = "Data Source=/var/lib/agentsmith/agentsmith.db";
    private const string EnvConnection =
        "Host=postgres;Database=agentsmith;Username=agentsmith;Password=from-the-secret";

    private readonly string _configPath =
        Path.Combine(Path.GetTempPath(), $"agentsmith-persistence-{Guid.NewGuid():N}.yml");

    private readonly string? _providerBefore =
        Environment.GetEnvironmentVariable(PersistenceEnvKeys.Provider);

    private readonly string? _connectionBefore =
        Environment.GetEnvironmentVariable(PersistenceEnvKeys.Connection);

    public PersistenceEnvironmentTests() => File.WriteAllText(_configPath, $"""
        persistence:
          provider: sqlite
          connection_string: "{FileConnection}"
        """);

    [Fact]
    public void BothVariables_ReachTheServerAndTheMigratingCli_Identically()
    {
        SetPair("postgresql", EnvConnection);

        var server = ReadBootstrap().Persistence;
        var cli = LoadFile().Persistence;

        server.Provider.Should().Be("postgresql");
        server.ConnectionString.Should().Be(EnvConnection);
        cli.Provider.Should().Be(server.Provider, "the init container migrates what the server opens");
        cli.ConnectionString.Should().Be(server.ConnectionString);
    }

    [Fact]
    public void BothVariables_WithNoFileAtAll_StillNameTheDatabase()
    {
        File.Delete(_configPath);
        SetPair("sqlserver", EnvConnection);

        var persistence = ReadBootstrap().Persistence;

        persistence.Provider.Should().Be("sqlserver");
        persistence.ConnectionString.Should().Be(
            EnvConnection, "a file that is missing must not silently retarget the server to SQLite");
    }

    [Fact]
    public void HalfAPair_ChangesNothing_AndNamesTheMissingVariable()
    {
        Environment.SetEnvironmentVariable(PersistenceEnvKeys.Connection, EnvConnection);
        var findings = new StartupFindings();

        var server = ReadBootstrap(findings).Persistence;
        var cli = LoadFile(findings).Persistence;

        server.ConnectionString.Should().Be(
            FileConnection, "the provider decides how a connection string is parsed");
        server.Provider.Should().Be("sqlite");
        cli.ConnectionString.Should().Be(FileConnection);
        findings.All.Should().Contain(f =>
            f.Severity == StartupFindingSeverity.Blocking
            && f.Subsystem == StartupSubsystems.ConfigFile
            && f.Field == PersistenceEnvKeys.Provider);
    }

    [Fact]
    public void BlankVariables_LeaveTheFilesBlockAlone()
    {
        SetPair("   ", "   ");

        ReadBootstrap().Persistence.ConnectionString.Should().Be(FileConnection);
        LoadFile().Persistence.ConnectionString.Should().Be(FileConnection);
    }

    [Fact]
    public void AnEnvironmentConnection_NeverReachesAConfigExport()
    {
        SetPair("postgresql", EnvConnection);

        var exported = new FileConfigStore(
            new FixedConfigPath(_configPath), new RawConfigYaml()).ExportYaml();

        exported.Should().NotContain("from-the-secret",
            "an export is the file's own text — a credential that only exists in the environment "
            + "must not be written into an artifact an operator moves around");
    }

    private BootstrapConfig ReadBootstrap(IStartupFindings? findings = null) =>
        new BootstrapConfigReader(
            new FixedConfigPath(_configPath), new RawConfigYaml(), new AuthEnvironmentOverlay(),
            new PersistenceEnvironmentOverlay(findings), findings).Read();

    private AgentSmithConfig LoadFile(IStartupFindings? findings = null) =>
        new YamlConfigurationLoader(
            new RawConfigMaterializer(
                new ProjectConfigNormalizer(), new EffectiveTriggerBuilder(),
                new DeploymentDefaultsApplier(), new ConfigCatalogResolver(), new AgentSmithPaths()),
            new NoOpSystemEventPublisher(),
            new PersistenceEnvironmentOverlay(findings)).LoadConfig(_configPath);

    private static void SetPair(string provider, string connection)
    {
        Environment.SetEnvironmentVariable(PersistenceEnvKeys.Provider, provider);
        Environment.SetEnvironmentVariable(PersistenceEnvKeys.Connection, connection);
    }

    private sealed record FixedConfigPath(string ConfigPath) : IConfigStoreLocation;

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(PersistenceEnvKeys.Provider, _providerBefore);
        Environment.SetEnvironmentVariable(PersistenceEnvKeys.Connection, _connectionBefore);
        if (File.Exists(_configPath)) File.Delete(_configPath);
    }
}
