using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Core.Services.Skills;
using FluentAssertions;
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
    private readonly string _root = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

    [Theory]
    [InlineData("principles")]                 // catalog >= 4.0.0
    [InlineData("skills/coding/principles")]   // catalog < 4.0.0
    public void Compose_EitherCatalogLayout_FindsTheTemplates(string subPath)
    {
        var dir = Path.Combine(_root, subPath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.Combine(dir, "deltas"));
        File.WriteAllText(Path.Combine(dir, "core.md"), "CORE RULES");
        File.WriteAllText(Path.Combine(dir, "deltas", "csharp.md"), "CSHARP DELTA");

        var composed = CreateSut().Compose("csharp");

        composed.Should().NotBeNull("a catalog shipping the templates must never read as pre-p0379");
        composed!.Content.Should().Contain("CORE RULES").And.Contain("CSHARP DELTA");
        composed.DeltaApplied.Should().BeTrue();
    }

    [Fact]
    public void Compose_NeitherLayoutPresent_ReturnsNull()
    {
        Directory.CreateDirectory(_root);

        CreateSut().Compose("csharp").Should().BeNull(
            "a catalog without the templates is a pre-p0379 pin, which is a legitimate state");
    }

    private CatalogPrinciplesTemplateSource CreateSut()
    {
        var path = new Mock<ISkillsCatalogPath>();
        path.Setup(p => p.Root).Returns(_root);
        return new CatalogPrinciplesTemplateSource(
            path.Object, NullLogger<CatalogPrinciplesTemplateSource>.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
