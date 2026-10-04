using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Core.Services.Skills;
using AgentSmith.Tests.Architecture;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.Skills;

/// <summary>
/// 2026-10-03-cf20c: the third layer of the composed principles. The fixtures are copies of the
/// catalog's spark overlay and scala delta (skills PR #210), because the embedded tarball the
/// goldens read carries neither.
/// </summary>
public sealed class FrameworkOverlayCompositionTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
    private readonly CapturingLogger<CatalogPrinciplesTemplateSource> _logger = new();

    public FrameworkOverlayCompositionTests()
    {
        var principles = Path.Combine(_root, "principles");
        Directory.CreateDirectory(Path.Combine(principles, "deltas"));
        Directory.CreateDirectory(Path.Combine(principles, "frameworks"));
        File.WriteAllText(Path.Combine(principles, "core.md"), "# Core\n\nCORE RULES\n");
        File.Copy(Fixture("scala.md"), Path.Combine(principles, "deltas", "scala.md"));
        File.Copy(Fixture("spark.md"), Path.Combine(principles, "frameworks", "spark.md"));
    }

    [Fact]
    public void CatalogPrinciples_OverlayApplied_RendersAllLanguagesAndTheLanguageSection()
    {
        var python = CreateSut().Compose("python", ["spark"])!;
        var scala = CreateSut().Compose("scala", ["spark"])!;

        python.Overlays.Should().Equal("spark");
        python.Content.Should().Contain("composed=core+python+spark")
            .And.Contain("## All languages").And.Contain("accumulator")
            .And.Contain("## python").And.Contain("F.lit(None)")
            .And.Contain("framework overlays 'spark'");
        python.Content.Should().NotContain("# Apache Spark Overlay", "the overlay's own title is not rendered")
            .And.NotContain("org.apache.spark", "detection is data for the detector, not a rule");
        scala.Content.Should().Contain("composed=core+scala+spark").And.Contain("# Scala Delta")
            .And.Contain("accumulator").And.NotContain("F.lit(None)",
                "only the section the component's language names is rendered");
        scala.Content.IndexOf("# Framework Overlay: spark", StringComparison.Ordinal)
            .Should().BeGreaterThan(scala.Content.IndexOf("# Scala Delta", StringComparison.Ordinal));
    }

    [Fact]
    public void FrameworkOverlays_CatalogOverlay_DeclaresItsSignals()
    {
        var spark = CreateSut().FrameworkOverlays().Should().ContainSingle().Subject;

        spark.Slug.Should().Be("spark");
        spark.Signals.Should().Contain(s => s.File == "build.sbt" && s.Contains == "org.apache.spark")
            .And.Contain(s => s.File == "requirements*.txt" && s.Contains == "pyspark");
    }

    [Fact]
    public void FrameworkOverlays_MalformedFile_IsLeftOutAndLogged()
    {
        File.WriteAllText(Path.Combine(_root, "principles", "frameworks", "broken.md"),
            "# Broken Overlay\n\n## Detection\n\n```yaml\n- file: /etc/passwd\n```\n");

        CreateSut().FrameworkOverlays().Select(o => o.Slug).Should().Equal("spark");
        CreateSut().Compose("scala", ["broken"])!.Overlays.Should().BeEmpty();
        _logger.Warnings.Should().Contain(w => w.Contains("broken.md", StringComparison.Ordinal));
    }

    [Fact]
    public void Compose_UnknownOverlay_ComposesWithoutIt()
    {
        var plain = CreateSut().Compose("scala", [])!;

        CreateSut().Compose("scala", ["flink"])!.Content.Should().Be(plain.Content);
        plain.Content.Should().Contain("composed=core+scala status").And.NotContain("Framework Overlay");
    }

    private static string Fixture(string name) =>
        Path.Combine(ArchitectureSources.TestSourceRoot, "Skills", "Fixtures", "FrameworkOverlays", name);

    private CatalogPrinciplesTemplateSource CreateSut()
    {
        var path = new Mock<ISkillsCatalogPath>();
        path.Setup(p => p.Root).Returns(_root);
        path.Setup(p => p.Origin).Returns("test catalog");
        return new CatalogPrinciplesTemplateSource(path.Object, _logger);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
