using AgentSmith.Application;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using AgentSmith.Infrastructure.Core.Services.Configuration.Studio;
using AgentSmith.Infrastructure.Extensions;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Extensions;
using AgentSmith.Infrastructure.Persistence.Models;
using AgentSmith.Infrastructure.Persistence.Services;
using AgentSmith.Server.Services;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Integration;

/// <summary>
/// p0198-followup: regression-guard for the composition-root bug that made
/// every `AgentSmithConfig`-injected handler see an EMPTY config in production.
///
/// AddAgentSmithCore registers `AgentSmithConfig.Empty()` as a placeholder
/// (Infrastructure.Core/ServiceCollectionExtensions.cs:33). The contract is
/// "composition roots replace it with the loader's output." Until the
/// override is wired in, every handler that depends on `AgentSmithConfig`
/// gets the empty placeholder — `config.Registries` is `[]`, `config.Repos`
/// is `{}`, etc.
///
/// The original symptom: `SetupRegistryAuthHandler` logs "No `registries:`
/// block in agentsmith.yml" even when the operator's YAML clearly contains
/// one. `AgenticMasterHandler.config.Registries` had the SAME bug since
/// p0191 but went unnoticed because the master never invoked the
/// `get_artifact_credentials` tool that consumes it.
///
/// Two paired tests:
///   1. Without the override → empty (documents the default; fails loudly
///      if anyone changes the default behavior).
///   2. With the override (the same line Server's Program.cs adds) →
///      loaded (proves the fix wires up).
/// </summary>
[Collection(EnvVarCollection.Name)]
public sealed class AgentSmithConfigCompositionTests : IDisposable
{
    private readonly string _fixturePath;

    public AgentSmithConfigCompositionTests()
    {
        _fixturePath = Path.Combine(Path.GetTempPath(),
            $"agentsmith-composition-{Guid.NewGuid():N}.yml");
        File.WriteAllText(_fixturePath, FixtureYaml);
        Environment.SetEnvironmentVariable("AZURE_DEVOPS_TOKEN", "test-token-xyz");
    }

    private string? _dbPath;

    public void Dispose()
    {
        try { File.Delete(_fixturePath); } catch { /* best-effort */ }
        if (_dbPath is not null) try { File.Delete(_dbPath); } catch { /* best-effort */ }
        if (_fixtureExtra is not null) try { File.Delete(_fixtureExtra); } catch { /* best-effort */ }
        Environment.SetEnvironmentVariable("AZURE_DEVOPS_TOKEN", null);
        Environment.SetEnvironmentVariable("CONFIG_PATH", null);
    }

    [Fact]
    public void Default_AgentSmithConfig_IsEmptyPlaceholder_NoRegistries()
    {
        // Documents the DEFAULT: AddAgentSmithCore registers Empty(). Any
        // handler resolved from this provider sees `config.Registries == []`.
        // If this test ever starts failing, the default registration changed
        // — update the override-pattern docs and the Program.cs fix line.
        using var provider = BuildBaseProvider();

        var config = provider.GetRequiredService<AgentSmithConfig>();

        config.Registries.Should().BeEmpty(
            "default DI registration is AgentSmithConfig.Empty(); composition roots MUST override.");
        config.Repos.Should().BeEmpty();
        config.Projects.Should().BeEmpty();
    }

    [Fact]
    public void ServerComposition_AgentSmithConfig_HasRegistriesFromDbStore()
    {
        // p0349: the server reads its config from the DB entity-document store, not
        // the file. Import the fixture into a DB, point the bootstrap at it, then the
        // ServerCompositionBuilder's DbConfigurationLoader must assemble the registries
        // (with the ${azure_devops_token} secret resolved) into the DI singleton.
        // Still guards the p0198 override: without it, AgentSmithConfig stays Empty().
        _dbPath = Path.Combine(Path.GetTempPath(), $"agentsmith-comp-{Guid.NewGuid():N}.db");
        ImportFixtureIntoDb(_dbPath);
        var bootstrapPath = WriteBootstrap(_dbPath);
        Environment.SetEnvironmentVariable("CONFIG_PATH", bootstrapPath);

        var services = new ServiceCollection();
        services.AddLogging(b => b.AddProvider(NullLoggerProvider.Instance));
        ServerCompositionBuilder.ConfigureServices(services, bootstrapPath);
        services.RemoveAll<StackExchange.Redis.IConnectionMultiplexer>();
        services.AddSingleton(Mock.Of<StackExchange.Redis.IConnectionMultiplexer>());
        services.RemoveAll<AgentSmith.Contracts.Events.ISystemEventPublisher>();
        services.AddSingleton<AgentSmith.Contracts.Events.ISystemEventPublisher,
            AgentSmith.Application.Services.Events.NoOpSystemEventPublisher>();

        using var provider = services.BuildServiceProvider();
        var config = provider.GetRequiredService<AgentSmithConfig>();

        config.Registries.Should().HaveCount(1,
            "the imported `registries:` must reach the DI singleton via the DB loader " +
            "so handlers (SetupRegistryAuthHandler, AgenticMasterHandler) see real values.");
        config.Registries[0].Host.Should().Be("pkgs.dev.azure.com");
        config.Registries[0].Token.Should().Be("test-token-xyz",
            "the `${azure_devops_token}` secret reference must resolve through the secrets dict.");
    }

    private void ImportFixtureIntoDb(string dbPath)
    {
        var builder = new DbContextOptionsBuilder<AgentSmithDbContext>();
        builder.UseProvider(new PersistenceOptions { Provider = PersistenceProvider.Sqlite, ConnectionString = $"Data Source={dbPath}" });
        using var db = new AgentSmithDbContext(builder.Options);
        db.Database.Migrate();
        var raw = RawConfigYaml.Deserialize(FixtureYaml);
        var writes = new ConfigDocumentAssembler().Decompose(raw)
            .Select(d => new ConfigDocWrite(d.Type, d.Id, d.Doc, null, d.Edges, "test"))
            .ToList();
        new ConfigImportRepository(db).Import(writes, force: false);
    }

    private string WriteBootstrap(string dbPath)
    {
        var path = Path.Combine(Path.GetTempPath(), $"agentsmith-bootstrap-{Guid.NewGuid():N}.yml");
        File.WriteAllText(path,
            $"persistence:\n  provider: sqlite\n  connection_string: 'Data Source={dbPath}'\n" +
            "secrets:\n  azure_devops_token: ${AZURE_DEVOPS_TOKEN}\n  gh_token: ${AZURE_DEVOPS_TOKEN}\n");
        _fixtureExtra = path;
        return path;
    }

    private string? _fixtureExtra;

    private static ServiceProvider BuildBaseProvider() =>
        BuildBaseServices().BuildServiceProvider();

    private static IServiceCollection BuildBaseServices()
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddProvider(NullLoggerProvider.Instance));
        services.AddAgentSmithInfrastructure();
        services.AddAgentSmithCommands();
        services.AddInProcessSandbox();
        services.AddSingleton(Mock.Of<IDialogueTransport>());
        services.AddSingleton(Mock.Of<IProgressReporter>());
        return services;
    }

    private const string FixtureYaml = """
        agents:
          openai-default:
            type: openai
            api_key: x
            models:
              primary:
                model: gpt-4

        trackers:
          gh:
            type: github
            url: https://github.com/x/y
            auth: gh_token

        repos:
          repo1:
            type: github
            url: https://github.com/x/y
            auth: gh_token

        projects:
          sample:
            agent: openai-default
            tracker: gh
            repos: [repo1]

        secrets:
          azure_devops_token: ${AZURE_DEVOPS_TOKEN}
          gh_token: ${AZURE_DEVOPS_TOKEN}

        registries:
          - host: pkgs.dev.azure.com
            username: any
            token: ${azure_devops_token}
        """;
}
