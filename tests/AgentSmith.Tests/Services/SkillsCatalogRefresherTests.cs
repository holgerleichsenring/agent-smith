using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Core.Services.Skills;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Services;

/// <summary>
/// p0358: a config-reload skills.version change pulls the catalog eagerly and
/// logs either way; an unchanged version never pulls; non-default sources and
/// pull failures are handled fail-soft.
/// </summary>
public sealed class SkillsCatalogRefresherTests
{
    private readonly Mock<ISkillsCatalogResolver> _resolver = new();
    private readonly Mock<ISkillsCacheMarker> _marker = new();

    [Fact]
    public async Task Refresh_VersionUnchanged_DoesNotPull()
    {
        _marker.Setup(m => m.Read(It.IsAny<string>())).Returns("v3.26.0");

        await NewSut().RefreshAsync(Skills("v3.26.0"), CancellationToken.None);

        _resolver.Verify(r => r.EnsureResolvedAsync(
            It.IsAny<SkillsConfig>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Refresh_VersionChanged_PullsEagerly()
    {
        _marker.Setup(m => m.Read(It.IsAny<string>())).Returns("v3.26.0");
        var skills = Skills("v3.27.0");
        _resolver.Setup(r => r.EnsureResolvedAsync(skills, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CatalogResolution(
                "/tmp/skills", "v3.27.0", SkillsSourceMode.Default, "url", FromCache: false));

        await NewSut().RefreshAsync(skills, CancellationToken.None);

        _resolver.Verify(r => r.EnsureResolvedAsync(skills, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Refresh_NoCacheMarker_Pulls()
    {
        _marker.Setup(m => m.Read(It.IsAny<string>())).Returns((string?)null);
        var skills = Skills("v3.27.0");
        _resolver.Setup(r => r.EnsureResolvedAsync(skills, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CatalogResolution(
                "/tmp/skills", "v3.27.0", SkillsSourceMode.Default, "url", FromCache: false));

        await NewSut().RefreshAsync(skills, CancellationToken.None);

        _resolver.Verify(r => r.EnsureResolvedAsync(skills, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Refresh_NonDefaultSource_SkipsWithoutPull()
    {
        var skills = new SkillsConfig { Source = SkillsSourceMode.Path, Version = "irrelevant" };

        await NewSut().RefreshAsync(skills, CancellationToken.None);

        _resolver.Verify(r => r.EnsureResolvedAsync(
            It.IsAny<SkillsConfig>(), It.IsAny<CancellationToken>()), Times.Never);
        _marker.Verify(m => m.Read(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Refresh_PullFails_LogsWarning_DoesNotThrow()
    {
        _marker.Setup(m => m.Read(It.IsAny<string>())).Returns("v3.26.0");
        _resolver.Setup(r => r.EnsureResolvedAsync(It.IsAny<SkillsConfig>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("404 on release asset"));
        var logger = new Mock<ILogger<SkillsCatalogRefresher>>();

        var act = async () => await new SkillsCatalogRefresher(
            _resolver.Object, _marker.Object, logger.Object)
            .RefreshAsync(Skills("v3.27.0"), CancellationToken.None);

        await act.Should().NotThrowAsync();
        logger.Verify(
            l => l.Log(
                LogLevel.Warning, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(), It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    private SkillsCatalogRefresher NewSut() => new(
        _resolver.Object, _marker.Object, NullLogger<SkillsCatalogRefresher>.Instance);

    private static SkillsConfig Skills(string version) => new()
    {
        Source = SkillsSourceMode.Default, Version = version, CacheDir = "/tmp/agentsmith/skills",
    };
}
