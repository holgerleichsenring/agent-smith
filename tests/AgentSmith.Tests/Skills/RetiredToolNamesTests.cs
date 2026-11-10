using System.Formats.Tar;
using System.IO.Compression;
using AgentSmith.Tests.Prompts;
using FluentAssertions;

namespace AgentSmith.Tests.Skills;

/// <summary>
/// 2026-11-10-85e2: no embedded skill names a tool that no master surface offers any more. v5.25.3 still made the deleted
/// read_sub_agent_observations a required step of mad-discussion and named it in the shared spawn-budget reference.
/// </summary>
public sealed class RetiredToolNamesTests
{
    /// <summary>Tools deleted from every surface; a skill that names one tells a model to call nothing.</summary>
    private static readonly string[] Retired = ["read_sub_agent_observations"];

    [Fact]
    public void EmbeddedCatalog_NoSkillOrReference_NamesARetiredTool()
    {
        var offenders = new List<string>();
        using var tarball = PackagedMaster.Catalog.Open();
        using var gz = new GZipStream(tarball, CompressionMode.Decompress);
        using var tar = new TarReader(gz);
        while (tar.GetNextEntry() is { DataStream: { } data } entry)
        {
            using var reader = new StreamReader(data);
            var text = reader.ReadToEnd();
            offenders.AddRange(Retired.Where(text.Contains).Select(tool => $"{entry.Name} names {tool}"));
        }

        offenders.Should().BeEmpty();
    }
}
