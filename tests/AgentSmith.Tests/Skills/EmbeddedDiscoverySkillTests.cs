using System.Formats.Tar;
using System.IO.Compression;
using AgentSmith.Infrastructure.Core.Services.Skills;
using FluentAssertions;

namespace AgentSmith.Tests.Skills;

/// <summary>
/// 2026-09-23-8d4d: the discovery skill's worked example is the SYSTEM half of the prompt
/// 2026-09-23-7868a corrected the user half of. A bundle whose example pairs a root workdir
/// with an entrypoint further down teaches the answer that phase exists to stop, and a pin
/// that moves backwards would restore it silently — the catalog is verified by checksum, and
/// a checksum says the bundle is the one named, not that the one named is right.
/// </summary>
public sealed class EmbeddedDiscoverySkillTests
{
    private const string DiscoverySkill = "skills/_masters/project-discovery/SKILL.md";

    [Fact]
    public void EmbeddedCatalog_TheDiscoveryExample_DoesNotCollapseASubTreeWorkdir()
    {
        var skill = Read(DiscoverySkill);

        skill.Should().Contain("\"workdir\": \"src/Sample.Cli\"",
            "the single-component example must show a component root below the repository root");
        skill.Should().NotContain("For single-component repos, return exactly one entry",
            "how many components a repository holds decides nothing about where one of them sits");
    }

    [Fact]
    public void EmbeddedCatalog_ItsDeclaredVersion_IsTheBundleItCarries()
    {
        var catalog = new EmbeddedSkillsCatalog();

        catalog.Version.Should().NotBeNullOrWhiteSpace();
        Read(DiscoverySkill).Should().NotBeEmpty(
            "the declared version is read from assembly metadata and the bundle from a resource; "
            + "a bump that moves one without the other leaves them disagreeing");
    }

    private static string Read(string path)
    {
        using var gzip = new GZipStream(new EmbeddedSkillsCatalog().Open(), CompressionMode.Decompress);
        using var tar = new TarReader(gzip);
        while (tar.GetNextEntry() is { } entry)
        {
            if (!string.Equals(entry.Name.TrimStart('.', '/'), path, StringComparison.Ordinal)) continue;
            using var data = new MemoryStream();
            entry.DataStream!.CopyTo(data);
            return System.Text.Encoding.UTF8.GetString(data.ToArray());
        }
        throw new InvalidOperationException($"'{path}' is not in the embedded catalog");
    }
}
