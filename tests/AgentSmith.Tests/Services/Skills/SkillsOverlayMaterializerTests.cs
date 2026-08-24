using AgentSmith.Application.Prompts;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Infrastructure.Core.Services.Skills;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Services.Skills;

/// <summary>
/// p0514: an operator catalog ADDS to the resolved one instead of replacing it.
/// Before this phase a configured directory became the catalog root wholesale and
/// the run lost every master the binary demands by name — these tests hold the
/// layering, the per-file precedence, the loud refusals and the caching.
/// </summary>
public sealed class SkillsOverlayMaterializerTests : IDisposable
{
    private readonly string _temp = Path.Combine(
        Path.GetTempPath(), $"agentsmith-overlay-{Guid.NewGuid():N}");

    private string BaseRoot => Path.Combine(_temp, "base");
    private string OverlayRoot => Path.Combine(_temp, "overlay");
    private string CacheDir => Path.Combine(_temp, "cache");
    private string LayeredRoot => CacheDir + "-overlay";

    public void Dispose()
    {
        try { Directory.Delete(_temp, recursive: true); } catch (IOException) { }
        try { Directory.Delete(LayeredRoot, recursive: true); } catch (IOException) { }
    }

    private static SkillsOverlayMaterializer Sut() =>
        new(NullLogger<SkillsOverlayMaterializer>.Instance);

    private static void WriteFile(string root, string relativePath, string content)
    {
        var path = Path.Combine(root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    // The masters the binary demands by literal name. A layered catalog that loses
    // one of these is the "Prompt resource not found" failure this phase removes.
    private static IReadOnlyList<string> OfficialMasters() =>
        [.. PromptOwnership.RequiredMasterSkills.Append(PipelinePresets.CodingMaster).Distinct()];

    private void GivenBaseCatalog()
    {
        foreach (var master in OfficialMasters())
            WriteFile(BaseRoot, Path.Combine("skills", "_masters", master, "SKILL.md"), $"base {master}");
        WriteFile(BaseRoot, Path.Combine("skills", "concept-vocabulary.yaml"), "concepts: []");
        WriteFile(BaseRoot, Path.Combine("references", "spawn-budget.md"), "base reference");
    }

    private void GivenOverlay(string relativePath = "skills/_masters/house-master/SKILL.md")
        => WriteFile(OverlayRoot, relativePath.Replace('/', Path.DirectorySeparatorChar), "overlay content");

    private SkillsConfig Config(string? overlay) =>
        new() { Source = SkillsSourceMode.Path, Path = BaseRoot, CacheDir = CacheDir, Overlay = overlay };

    private CatalogResolution BaseResolution() =>
        new(BaseRoot, "v4.6.0", SkillsSourceMode.Path, BaseRoot, FromCache: true);

    [Fact]
    public void Overlay_NotConfigured_TheResolvedRootIsTheBaseUnchanged()
    {
        GivenBaseCatalog();
        var basis = BaseResolution();

        var result = Sut().Apply(basis, Config(overlay: null));

        result.Should().BeSameAs(basis, "an unconfigured overlay resolves byte-for-byte as before");
        Directory.Exists(LayeredRoot).Should().BeFalse("nothing is materialized without an overlay");
    }

    [Fact]
    public void Overlay_Configured_TheOfficialMastersAreAllStillPresent()
    {
        GivenBaseCatalog();
        GivenOverlay();

        var result = Sut().Apply(BaseResolution(), Config(OverlayRoot));

        foreach (var master in OfficialMasters())
        {
            File.Exists(Path.Combine(result.Root, "skills", "_masters", master, "SKILL.md"))
                .Should().BeTrue($"'{master}' comes from the pinned catalog the operator did not write");
        }

        File.Exists(Path.Combine(result.Root, "skills", "concept-vocabulary.yaml")).Should().BeTrue();
        File.Exists(Path.Combine(result.Root, "references", "spawn-budget.md")).Should().BeTrue();
    }

    [Fact]
    public void Overlay_CarriesASkillTheBaseDoesNot_ItIsInTheResolvedRoot()
    {
        GivenBaseCatalog();
        GivenOverlay();

        var result = Sut().Apply(BaseResolution(), Config(OverlayRoot));

        File.ReadAllText(Path.Combine(result.Root, "skills", "_masters", "house-master", "SKILL.md"))
            .Should().Be("overlay content");
    }

    [Fact]
    public void Overlay_CarriesAFileTheBaseAlsoHas_TheOverlayWins()
    {
        GivenBaseCatalog();
        var shadowed = Path.Combine("skills", "_masters", PipelinePresets.CodingMaster, "SKILL.md");
        WriteFile(OverlayRoot, shadowed, "operator coding master");

        var result = Sut().Apply(BaseResolution(), Config(OverlayRoot));

        File.ReadAllText(Path.Combine(result.Root, shadowed))
            .Should().Be("operator coding master", "replacing a master is allowed and is the point");
    }

    [Fact]
    public void Overlay_MissingDirectory_Throws()
    {
        GivenBaseCatalog();

        var act = () => Sut().Apply(BaseResolution(), Config(Path.Combine(_temp, "nowhere")));

        act.Should().Throw<DirectoryNotFoundException>().WithMessage("*skills.overlay*does not exist*");
    }

    [Fact]
    public void Overlay_DirectoryWithoutASkillsSubtree_Throws()
    {
        GivenBaseCatalog();
        WriteFile(OverlayRoot, "README.md", "no skills subtree here");

        var act = () => Sut().Apply(BaseResolution(), Config(OverlayRoot));

        act.Should().Throw<DirectoryNotFoundException>().WithMessage("*must contain a 'skills/' subdirectory*");
    }

    [Fact]
    public void Overlay_Unchanged_TheSecondResolveReusesTheMaterialisedRoot()
    {
        GivenBaseCatalog();
        GivenOverlay();
        var first = Sut().Apply(BaseResolution(), Config(OverlayRoot));
        var sentinel = Path.Combine(first.Root, "sentinel.txt");
        File.WriteAllText(sentinel, "survives a cache hit");

        var second = Sut().Apply(BaseResolution(), Config(OverlayRoot));

        second.Root.Should().Be(first.Root);
        second.Overlay.Should().Be(first.Overlay);
        File.Exists(sentinel).Should().BeTrue("an unchanged overlay is not re-copied");
    }

    [Fact]
    public void Overlay_FileAdded_TheNextResolveRematerialises()
    {
        GivenBaseCatalog();
        GivenOverlay();
        var first = Sut().Apply(BaseResolution(), Config(OverlayRoot));
        var sentinel = Path.Combine(first.Root, "sentinel.txt");
        File.WriteAllText(sentinel, "must not survive a re-materialise");
        GivenOverlay("skills/_masters/second-house-master/SKILL.md");

        var second = Sut().Apply(BaseResolution(), Config(OverlayRoot));

        second.Overlay.Should().NotBe(first.Overlay, "the overlay's file set changed");
        File.Exists(Path.Combine(second.Root, "skills", "_masters", "second-house-master", "SKILL.md"))
            .Should().BeTrue("an edited overlay takes effect on the next resolve");
        File.Exists(sentinel).Should().BeFalse("the layered root is rebuilt, not patched");
    }
}
