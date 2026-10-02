using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Server.Services.Config;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace AgentSmith.Tests.DesignSources;

/// <summary>
/// 2026-10-01-7f7aa: a secret's value is looked up at use, and the masker reads the values
/// the configuration holds NOW — a secret added in the Studio after startup is masked like
/// one known at boot. The CLI has no Studio: its values are those of the file it loaded.
/// </summary>
public sealed class SecretValuesTests
{
    private const string BootToken = "boot-token-123456";
    private const string StudioToken = "figd-studio-added-987654";

    [Fact]
    public void SecretMasker_SecretAddedInStudioAfterStartup_IsMasked()
    {
        var (store, loader) = StoreAndLoader();
        var masker = new SecretMasker(new CurrentSecretValues(store.Object, loader.Object, new ServerContext("x")));
        masker.Apply($"a {BootToken}").Should().Be("a ***");

        store.Setup(s => s.Catalog).Returns(new ConfigCatalog());
        loader.Setup(l => l.LoadConfig(It.IsAny<string>())).Returns(Config(("BOOT", BootToken), ("FIGMA", StudioToken)));

        masker.Apply($"token={StudioToken}").Should().Be("token=***");
    }

    [Fact]
    public void CurrentSecretValues_CatalogUnchanged_DoesNotReload()
    {
        var (store, loader) = StoreAndLoader();
        var values = new CurrentSecretValues(store.Object, loader.Object, new ServerContext("x"));

        values.Resolve("BOOT").Should().Be(BootToken);
        values.All().Should().BeSameAs(values.All());

        loader.Verify(l => l.LoadConfig(It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public void LoadedSecretValues_ResolvesByNameIgnoringCase_AndListsRegistryTokens()
    {
        var values = new LoadedSecretValues(new AgentSmithConfig
        {
            Secrets = new Dictionary<string, string> { ["FIGMA_TOKEN"] = StudioToken, ["EMPTY"] = "" },
            Registries = [new RegistryConfig("packages.example.test", "ci", "registry-token-1")],
        });

        values.Resolve("figma_token").Should().Be(StudioToken);
        values.Resolve("EMPTY").Should().BeNull("an empty value is no token");
        values.Resolve("UNKNOWN").Should().BeNull();
        values.All().Should().BeEquivalentTo(StudioToken, "registry-token-1");
    }

    [Fact]
    public void CliProvider_SecretValues_AreThoseOfTheLoadedFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"agentsmith-cli-{Guid.NewGuid():N}.yml");
        File.WriteAllText(path, """
            agents:
              default: { type: claude, model: sonnet }
            secrets:
              figma_token: figd-file-literal-token
            """);
        try
        {
            using var provider = AgentSmith.Cli.ServiceProviderFactory.Build(
                configPath: path, verbose: false, headless: true);

            var values = provider.GetRequiredService<ISecretValues>();

            values.Should().BeOfType<LoadedSecretValues>("the CLI's configuration is the file it read once");
            values.Resolve("figma_token").Should().Be("figd-file-literal-token");
        }
        finally { File.Delete(path); }
    }

    private static (Mock<IConfigStore>, Mock<IConfigurationLoader>) StoreAndLoader()
    {
        var store = new Mock<IConfigStore>();
        store.Setup(s => s.Catalog).Returns(new ConfigCatalog());
        var loader = new Mock<IConfigurationLoader>();
        loader.Setup(l => l.LoadConfig(It.IsAny<string>())).Returns(Config(("BOOT", BootToken)));
        return (store, loader);
    }

    private static AgentSmithConfig Config(params (string Name, string Value)[] secrets) =>
        new() { Secrets = secrets.ToDictionary(s => s.Name, s => s.Value) };
}
