using AgentSmith.Application.Models.Sandbox;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Server.Services;
using AgentSmith.Server.Services.Config;
using AgentSmith.Server.Services.Sandbox;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Server;

/// <summary>
/// 2026-10-12-6bfab: the server's image boundary is read from the config store per decision,
/// never from the once-built options and never from the loader that degrades to empty.
/// </summary>
[Trait("TestProcess", "env-1")]
[Collection(TestSupport.EnvVarCollection.Name)]
public sealed class StoredSandboxSettingsReaderTests
{
    [Fact]
    public void Current_ReadsTheStoredSandboxSetting()
    {
        var stored = new SandboxGlobalConfig { AllowedRegistries = ["docker.io/sbtscala/"] };
        var store = new Mock<IConfigStore>();
        store.Setup(s => s.GetSetting("sandbox")).Returns(stored);

        new StoredSandboxSettingsReader(store.Object).Current().Should().BeSameAs(stored);
    }

    [Fact]
    public void Current_StoreCannotRead_Throws()
    {
        var store = new Mock<IConfigStore>();
        store.Setup(s => s.GetSetting("sandbox")).Throws(new InvalidOperationException("db down"));

        var read = () => new StoredSandboxSettingsReader(store.Object).Current();

        read.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ServerComposition_ImageBoundary_ReadsTheStore()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddProvider(NullLoggerProvider.Instance));
        ServerCompositionBuilder.ConfigureServices(services, "agentsmith.yml");

        services.Where(d => d.ServiceType == typeof(ISandboxSettingsReader)).Should().ContainSingle()
            .Which.ImplementationType.Should().Be(typeof(StoredSandboxSettingsReader));
        services.Should().Contain(d => d.ServiceType == typeof(IImageRegistryTrust));
    }

    [Fact]
    public void Map_CarriesTheBoundaryAndTheBuiltInDefault()
    {
        var response = ImageTrustResponse.Map(RegistryTrustBoundary.From(
            new SandboxGlobalConfig { AllowedRegistries = ["sbtscala/"] }));

        response.Registries.Should().Equal("docker.io/sbtscala/");
        response.RegistriesAreDefault.Should().BeFalse();
        response.DockerHubLibrary.Should().BeFalse();
        response.DockerHubLibraryIsExplicit.Should().BeFalse();
        response.DefaultRegistries.Should().Equal("mcr.microsoft.com/", "ghcr.io/");
        response.Description.Should().Contain("docker.io/sbtscala/");
    }
}
