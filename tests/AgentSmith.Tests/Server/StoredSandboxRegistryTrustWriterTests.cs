using System.Text.Json;
using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Infrastructure.Core.Services.Configuration.Studio;
using AgentSmith.Server.Services.Sandbox;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Server;

/// <summary>
/// 2026-11-16-f3fbd: a run's registry trust lands in the stored sandbox block through the settings save,
/// attributed to the run, once per prefix, retried when another write moved the version on, and signalled.
/// </summary>
public sealed class StoredSandboxRegistryTrustWriterTests
{
    private readonly Mock<IConfigStore> _store = new();
    private readonly Mock<IConfigReloadSignal> _reload = new();
    private readonly List<(SandboxGlobalConfig Doc, string Actor)> _saves = [];
    private SandboxGlobalConfig _stored = new() { AllowedRegistries = ["ghcr.io/"], AgentVersion = "1.2.3" };

    public StoredSandboxRegistryTrustWriterTests()
    {
        _store.Setup(s => s.GetSetting(ConfigDocTypes.Sandbox)).Returns(() => _stored);
        _store.Setup(s => s.SaveSetting(ConfigDocTypes.Sandbox, It.IsAny<JsonElement>(), It.IsAny<ChangeAttribution>()))
            .Callback<string, JsonElement, ChangeAttribution>((_, doc, by) =>
            {
                _stored = doc.Deserialize<SandboxGlobalConfig>(new ConfigDocJson().Options)!;
                _saves.Add((_stored, by.Actor));
            });
    }

    private StoredSandboxRegistryTrustWriter Writer() =>
        new(_store.Object, _reload.Object, new ConfigDocJson(), NullLogger<StoredSandboxRegistryTrustWriter>.Instance);

    [Fact]
    public async Task TrustAsync_NewPrefix_AppendsItOnceWithTheRunAsActorAndBumpsTheEpoch()
    {
        var first = await Writer().TrustAsync("docker.io/library/", "run-7", CancellationToken.None);
        var second = await Writer().TrustAsync("docker.io/library/", "run-8", CancellationToken.None);

        first.Trusted.Should().BeTrue();
        second.Trusted.Should().BeTrue("the entry is listed already");
        _saves.Should().ContainSingle().Which.Actor.Should().Be("run run-7");
        _stored.AllowedRegistries.Should().Equal("ghcr.io/", "docker.io/library/");
        _stored.AgentVersion.Should().Be("1.2.3", "the rest of the block is saved as it was");
        _reload.Verify(r => r.BumpAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TrustAsync_EmptyList_StartsFromTheBuiltInDefault()
    {
        _stored = new SandboxGlobalConfig();

        await Writer().TrustAsync("docker.io/sbtscala/", "run-7", CancellationToken.None);

        _stored.AllowedRegistries.Should().Equal(
            "mcr.microsoft.com/", "ghcr.io/", "docker.io/library/", "docker.io/sbtscala/");
    }

    [Fact]
    public async Task TrustAsync_StaleVersion_ReloadsAndRetries()
    {
        var calls = 0;
        _store.Setup(s => s.SaveSetting(ConfigDocTypes.Sandbox, It.IsAny<JsonElement>(), It.IsAny<ChangeAttribution>()))
            .Callback<string, JsonElement, ChangeAttribution>((_, doc, by) =>
            {
                if (calls++ == 0) throw new StaleConfigVersionException(ConfigDocTypes.Sandbox, "default", 1, 2);
                _stored = doc.Deserialize<SandboxGlobalConfig>(new ConfigDocJson().Options)!;
            });

        var write = await Writer().TrustAsync("docker.io/library/", "run-7", CancellationToken.None);

        write.Trusted.Should().BeTrue();
        calls.Should().Be(2);
        _store.Verify(s => s.Load(), Times.Once);
        _stored.AllowedRegistries.Should().Contain("docker.io/library/");
    }

    [Fact]
    public async Task TrustAsync_ReadOnlyStore_RefusesNamingTheSetting()
    {
        _store.Setup(s => s.SaveSetting(ConfigDocTypes.Sandbox, It.IsAny<JsonElement>(), It.IsAny<ChangeAttribution>()))
            .Throws(new NotSupportedException("read-only"));

        var write = await Writer().TrustAsync("docker.io/library/", "run-7", CancellationToken.None);

        write.Trusted.Should().BeFalse();
        write.Message.Should().Contain("sandbox.allow_new_registries");
    }
}
