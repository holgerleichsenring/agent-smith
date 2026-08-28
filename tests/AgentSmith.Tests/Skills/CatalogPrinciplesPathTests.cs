using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Core.Services.Skills;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Skills;

/// <summary>
/// p0312a moved the p0379 principles templates from skills/coding/principles to
/// the catalog root. The backend and the catalog version move independently — the
/// pin is operator configuration — so a 4.0.0 binary can face a 3.x catalog and the
/// other way round.
///
/// This reader degrades SILENTLY: a missing directory returns null, which callers
/// read as "pre-p0379 catalog" and carry on. So a path mismatch does not fail, it
/// removes the coding principles from every run with nobody told. Both layouts are
/// therefore probed, and both are pinned here.
/// </summary>
public sealed class CatalogPrinciplesPathTests : IDisposable
{
    private const string Origin = "embedded v4.7.0 at /stub";

    private readonly string _root = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

    [Fact]
    public void Compose_TheCatalogLayout_FindsTheTemplates()
    {
        // 2026-08-28-489a: one layout. The pre-4.0.0 location was a case here until the
        // supported floor became the release that carries the current one.
        var dir = Path.Combine(_root, "principles");
        Directory.CreateDirectory(Path.Combine(dir, "deltas"));
        File.WriteAllText(Path.Combine(dir, "core.md"), "CORE RULES");
        File.WriteAllText(Path.Combine(dir, "deltas", "csharp.md"), "CSHARP DELTA");

        var composed = CreateSut().Compose("csharp");

        composed.Should().NotBeNull("a catalog shipping the templates must never read as pre-p0379");
        composed!.Content.Should().Contain("CORE RULES").And.Contain("CSHARP DELTA");
        composed.DeltaApplied.Should().BeTrue();
    }

    [Fact]
    public void Compose_NoPrinciplesPresent_ReturnsNull()
    {
        Directory.CreateDirectory(_root);

        CreateSut().Compose("csharp").Should().BeNull(
            "a catalog shipping no principles hands authorship to the bootstrap skill");
    }

    [Fact]
    public void Compose_TheLegacyLocation_IsNotProbed()
    {
        // A catalog laid out the pre-4.0.0 way is below the supported floor, and reading it
        // would keep alive the one path that made an old pin look like a current one.
        var legacy = Path.Combine(_root, "skills", "coding", "principles");
        Directory.CreateDirectory(legacy);
        File.WriteAllText(Path.Combine(legacy, "core.md"), "CORE RULES");

        CreateSut().Compose("csharp").Should().BeNull();
    }

    [Fact]
    public void Compose_ACatalogWithoutPrinciples_ReportsTheOriginItRead()
    {
        // 2026-08-28-7675: null hands principles authorship to the bootstrap skill, which is
        // a mode rather than a fault — but it used to happen without a word, so a run whose
        // principles the skill wrote read exactly like one that got the authored core.
        Directory.CreateDirectory(_root);
        var logger = new CapturingLogger<CatalogPrinciplesTemplateSource>();

        CreateSut(logger).Compose("csharp").Should().BeNull();

        logger.Lines.Should().ContainSingle(l => l.Contains(Origin, StringComparison.Ordinal)
            && l.Contains("bootstrap skill authors", StringComparison.Ordinal));
    }

    private CatalogPrinciplesTemplateSource CreateSut(
        ILogger<CatalogPrinciplesTemplateSource>? logger = null)
    {
        var path = new Mock<ISkillsCatalogPath>();
        path.Setup(p => p.Root).Returns(_root);
        path.Setup(p => p.Origin).Returns(Origin);
        return new CatalogPrinciplesTemplateSource(
            path.Object, logger ?? NullLogger<CatalogPrinciplesTemplateSource>.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
