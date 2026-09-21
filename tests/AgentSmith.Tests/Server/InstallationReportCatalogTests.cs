using System.Text.Json;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Core.Services.Skills;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.Diagnostics;
using AgentSmith.Server.Services.Startup;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace AgentSmith.Tests.Server;

/// <summary>
/// 2026-09-20-4981: which catalog an installation is on, read WITHOUT signing in. The
/// catalog endpoint answers the same question behind a catalog permission and goes silent
/// (not-ready, null binding) when the source is unavailable — which is the moment the
/// question is asked. So the anonymous report carries two facts that differ by design on
/// three of the four source modes: the binding this server RESOLVED, and the version the
/// binary embeds, as the floor it was built against.
/// </summary>
public sealed class InstallationReportCatalogTests
{
    private const string Release = "1.2.3";
    private const string Revision = "1111111111111111111111111111111111111111";
    private const string EmbeddedFloor = "v4.6.0";
    private const string PulledRelease = "v5.1.0";
    private const string OverlayFingerprint = "9f2c1ab4";

    [Fact]
    public async Task InstallationReport_ReportsTheResolvedCatalogBindingAndTheEmbeddedFloor()
    {
        var resolution = Resolution(SkillsSourceMode.Embedded, EmbeddedFloor, "/var/lib/agentsmith/skills");

        var report = await Reporter(resolution, Configured(SkillsSourceMode.Embedded, null)).ReadAsync(default);

        report.Catalog.Source.Should().Be("embedded");
        report.Catalog.Version.Should().Be(EmbeddedFloor);
        report.Catalog.Resolved.Should().BeTrue();
        report.EmbeddedCatalogVersion.Should().Be(EmbeddedFloor,
            "the floor is reported in its own right, not inferred from the binding");
    }

    [Fact]
    public async Task InstallationReport_AResolvedCatalogOtherThanTheEmbeddedOne_ReportsBothAndTheyDiffer()
    {
        // The shipped example configuration pins a release, so this is the ordinary case:
        // the binary embeds one catalog and the server runs another, with an overlay on top.
        var resolution = Resolution(
            SkillsSourceMode.Default, PulledRelease, "/var/cache/agentsmith/skills-overlay",
            OverlayFingerprint);

        var report = await Reporter(resolution, Configured(SkillsSourceMode.Default, PulledRelease))
            .ReadAsync(default);

        report.Catalog.Source.Should().Be("default");
        report.Catalog.Version.Should().Be(PulledRelease);
        report.Catalog.Overlay.Should().Be(OverlayFingerprint,
            "a base version alone tells a half-truth when an overlay replaced a master");
        report.Catalog.Resolved.Should().BeTrue();
        report.EmbeddedCatalogVersion.Should().Be(EmbeddedFloor);
        report.EmbeddedCatalogVersion.Should().NotBe(report.Catalog.Version,
            "reporting the embedded pin as THE catalog would be a confident false answer");
    }

    [Fact]
    public async Task InstallationReport_ACatalogThatWillNotResolve_StillNamesWhatWasConfigured()
    {
        // Nothing ever resolved — an unreachable source, a wrong pin, a missing mount. This
        // is when an operator reads this page, so it states the configuration and says the
        // resolution is not fact.
        var report = await Reporter(resolution: null, Configured(SkillsSourceMode.Default, PulledRelease))
            .ReadAsync(default);

        report.Catalog.Source.Should().Be("default");
        report.Catalog.Version.Should().Be(PulledRelease);
        report.Catalog.Resolved.Should().BeFalse("what was configured is not what was bound");
        report.EmbeddedCatalogVersion.Should().Be(EmbeddedFloor,
            "the floor is a build constant and is knowable even when nothing resolved");
    }

    [Fact]
    public async Task InstallationReport_TheBinding_CarriesNoFilesystemRoot()
    {
        const string root = "/srv/mounted-catalog-root-marker";
        const string overlayDirectory = "/srv/operator-overlay-directory-marker";
        var resolution = Resolution(SkillsSourceMode.Path, "local", root);
        var configured = Configured(SkillsSourceMode.Path, null);
        configured.Path = root;
        configured.Overlay = overlayDirectory;

        var resolved = JsonSerializer.Serialize(await Reporter(resolution, configured).ReadAsync(default));
        var unresolved = JsonSerializer.Serialize(await Reporter(null, configured).ReadAsync(default));

        resolution.Origin.Should().Contain(root,
            "the root IS available here — this surface drops it rather than never having it");
        resolved.Should().NotContain(root).And.NotContain(overlayDirectory,
            "this report is read anonymously and a mounted catalog's root is an operator's own directory");
        unresolved.Should().NotContain(root).And.NotContain(overlayDirectory);
    }

    private static CatalogResolution Resolution(
        SkillsSourceMode source, string version, string root, string? overlay = null) =>
        new(root, version, source, SourceUrl: "https://example.invalid/catalog.tar.gz",
            FromCache: true, overlay);

    private static SkillsConfig Configured(SkillsSourceMode source, string? version) =>
        new() { Source = source, Version = version };

    private static InstallationIdentityReporter Reporter(CatalogResolution? resolution, SkillsConfig skills)
    {
        // The real holder the resolver publishes to, so this pins the path the server
        // actually reads and not a stub's idea of it.
        var binding = new SkillsCatalogPath();
        if (resolution is not null) binding.Set(resolution);

        var config = new AgentSmithConfig
        {
            Persistence = new PersistenceConfig { Provider = "sqlite" },
            Skills = skills,
        };
        return new InstallationIdentityReporter(
            new BuildIdentity(Revision, Release), config,
            new AgentVersionResolver(
                Options.Create(new SandboxGlobalConfig { AgentVersion = string.Empty }),
                new BuildIdentity(Revision, Release)),
            Persistence(), binding, Embedded(),
            NullLogger<InstallationIdentityReporter>.Instance);
    }

    private static IEmbeddedSkillsCatalog Embedded()
    {
        var catalog = new Mock<IEmbeddedSkillsCatalog>();
        catalog.SetupGet(c => c.Version).Returns(EmbeddedFloor);
        return catalog.Object;
    }

    private static IPersistenceStateReader Persistence()
    {
        var reader = new Mock<IPersistenceStateReader>();
        reader.Setup(r => r.ReadPersistenceStateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PersistenceState(true, 0, null));
        return reader.Object;
    }
}
