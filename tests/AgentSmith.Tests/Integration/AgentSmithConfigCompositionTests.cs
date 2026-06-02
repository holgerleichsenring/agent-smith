using AgentSmith.Application;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure;
using AgentSmith.Infrastructure.Extensions;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
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

    public void Dispose()
    {
        try { File.Delete(_fixturePath); } catch { /* best-effort */ }
        Environment.SetEnvironmentVariable("AZURE_DEVOPS_TOKEN", null);
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
    public void WithLoaderOverride_AgentSmithConfig_HasRegistriesFromYaml()
    {
        // The override pattern Server's Program.cs uses to make
        // operator-set blocks actually reach handlers that inject
        // AgentSmithConfig (registries, pipeline_cost_cap, …).
        var configPath = _fixturePath;
        var services = BuildBaseServices();
        services.AddSingleton<AgentSmithConfig>(sp =>
            sp.GetRequiredService<IConfigurationLoader>().LoadConfig(configPath));

        using var provider = services.BuildServiceProvider();
        var config = provider.GetRequiredService<AgentSmithConfig>();

        config.Registries.Should().HaveCount(1,
            "operator's `registries:` block in agentsmith.yml must reach the DI singleton " +
            "so handlers (SetupRegistryAuthHandler, AgenticMasterHandler) see real values.");
        config.Registries[0].Host.Should().Be("pkgs.dev.azure.com");
        config.Registries[0].Token.Should().Be("test-token-xyz",
            "the `${azure_devops_token}` secret reference must resolve through the secrets dict.");
    }

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
