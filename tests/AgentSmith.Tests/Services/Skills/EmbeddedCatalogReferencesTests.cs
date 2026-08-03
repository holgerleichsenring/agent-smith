using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Skills;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Core.Services;
using AgentSmith.Infrastructure.Core.Services.Skills;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Services.Skills;

/// <summary>
/// p0313b: the catalog packages references/ alongside skills/, and a master that
/// cites {{ref:&lt;slug&gt;}} does not render without it. Those are two repositories
/// moving independently — the citation is authored in agent-smith-skills, the
/// packaging list is a shell script there, and the pin is here.
///
/// This runs the REAL embedded tarball through the REAL resolver: every master the
/// pinned catalog ships must resolve. A pin whose masters cite references the
/// tarball does not carry fails here, at build time, instead of at the first run
/// that loads that master.
/// </summary>
public sealed class EmbeddedCatalogReferencesTests : IDisposable
{
    private readonly string _cacheDir = Path.Combine(Path.GetTempPath(),
        $"agentsmith-refs-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_cacheDir)) Directory.Delete(_cacheDir, recursive: true);
    }

    [Fact]
    public async Task EmbeddedCatalog_EveryMaster_ResolvesItsCitedReferences()
    {
        var root = await MaterializeAsync();
        var catalogPath = new Mock<ISkillsCatalogPath>();
        catalogPath.Setup(p => p.Root).Returns(root);
        var resolver = new SkillBodyResolver(new CatalogSkillReferenceSource(catalogPath.Object));

        var masters = Directory.GetFiles(
            Path.Combine(root, "skills", "_masters"), "SKILL.md", SearchOption.AllDirectories);
        masters.Should().NotBeEmpty("the pinned catalog must ship masters at all");

        foreach (var master in masters)
        {
            var name = Path.GetFileName(Path.GetDirectoryName(master))!;
            var resolve = () => resolver.ResolveBody(
                new RoleSkillDefinition { Name = name, Rules = File.ReadAllText(master) },
                SkillRole.Master);

            resolve.Should().NotThrow(
                $"master '{name}' must render against the catalog that ships it");
        }
    }

    private async Task<string> MaterializeAsync()
    {
        var handler = new EmbeddedSourceHandler(
            new EmbeddedSkillsCatalog(),
            new CatalogTarballExtractor(NullLogger<CatalogTarballExtractor>.Instance),
            new SkillsCacheMarker(NullLogger<SkillsCacheMarker>.Instance),
            NullLogger<EmbeddedSourceHandler>.Instance);
        var resolution = await handler.ResolveAsync(
            new SkillsConfig { Source = SkillsSourceMode.Embedded, CacheDir = _cacheDir },
            CancellationToken.None);
        return resolution.Root;
    }
}
