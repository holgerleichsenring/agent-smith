using AgentSmith.Infrastructure.Core.Services.Skills;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.PipelineHarness.Evals;

/// <summary>
/// 2026-10-02-5f89f: the eval catalog is the test process's own extraction, so a temp
/// cleaner that emptied an earlier one cannot make the scan load zero pattern definitions.
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class EmbeddedSkillsCatalogPathTests
{
    [Fact]
    public void EmbeddedSkillsCatalogPath_AnEmptiedEarlierExtraction_IsNotReused()
    {
        var catalog = new EmbeddedSkillsCatalog();
        var emptied = Path.Combine(
            Path.GetTempPath(), $"agentsmith-eval-catalog-{catalog.Version}");
        var leftBehind = !Directory.Exists(emptied);
        Directory.CreateDirectory(Path.Combine(emptied, "patterns"));
        try
        {
            var path = new EmbeddedSkillsCatalogPath(
                catalog, new CatalogTarballExtractor(NullLogger<CatalogTarballExtractor>.Instance));

            path.Root.Should().NotBe(emptied,
                "a directory whose files a temp cleaner removed says nothing about its contents");
            Directory.EnumerateFiles(Path.Combine(path.Root, "patterns"), "*", SearchOption.AllDirectories)
                .Should().NotBeEmpty("the process's own extraction carries the pattern definitions");
        }
        finally
        {
            if (leftBehind) Directory.Delete(emptied, recursive: true);
        }
    }
}
