using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Server.Services.Config;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.Configuration;

/// <summary>2026-11-17-274ca: the server's configuration follows the store's catalog instance.</summary>
public sealed class CurrentConfigSnapshotTests
{
    [Fact]
    public void Value_NewCatalogInstance_LoadsAgain()
    {
        var store = new Mock<IConfigStore>();
        store.Setup(s => s.Catalog).Returns(new ConfigCatalog());
        var loader = new Mock<IConfigurationLoader>();
        loader.Setup(l => l.LoadConfig(It.IsAny<string>())).Returns(WithSeconds(60));
        var snapshot = new CurrentConfigSnapshot(store.Object, loader.Object, new ServerContext("x"));
        snapshot.Value.Limits.MaxSecondsPerSkillCall.Should().Be(60);

        store.Setup(s => s.Catalog).Returns(new ConfigCatalog());
        loader.Setup(l => l.LoadConfig(It.IsAny<string>())).Returns(WithSeconds(90));

        snapshot.Value.Limits.MaxSecondsPerSkillCall.Should().Be(90, "a Studio save replaces the catalog instance");
    }

    [Fact]
    public void Value_SameCatalogInstance_LoadsOnce()
    {
        var store = new Mock<IConfigStore>();
        store.Setup(s => s.Catalog).Returns(new ConfigCatalog());
        var loader = new Mock<IConfigurationLoader>();
        loader.Setup(l => l.LoadConfig(It.IsAny<string>())).Returns(WithSeconds(60));
        var snapshot = new CurrentConfigSnapshot(store.Object, loader.Object, new ServerContext("x"));

        snapshot.Value.Should().BeSameAs(snapshot.Value);

        loader.Verify(l => l.LoadConfig(It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public void Value_UnreadableStore_IsWhatTheLoaderDegradesTo_LoadedOnce()
    {
        var store = new Mock<IConfigStore>();
        store.Setup(s => s.Catalog).Throws(new InvalidOperationException("no database"));
        var loader = new Mock<IConfigurationLoader>();
        loader.Setup(l => l.LoadConfig(It.IsAny<string>())).Returns(AgentSmithConfig.Empty());
        var snapshot = new CurrentConfigSnapshot(store.Object, loader.Object, new ServerContext("x"));

        snapshot.Value.Limits.MaxSecondsPerSkillCall.Should().Be(300);
        _ = snapshot.Value;

        loader.Verify(l => l.LoadConfig(It.IsAny<string>()), Times.Once);
    }

    private static AgentSmithConfig WithSeconds(int seconds) =>
        new() { Limits = new LoopLimitsConfig { MaxSecondsPerSkillCall = seconds } };
}
