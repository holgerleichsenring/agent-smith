using System.Formats.Tar;
using System.IO.Compression;
using AgentSmith.Infrastructure.Core.Services.Skills;
using FluentAssertions;

namespace AgentSmith.Tests.Prompts;

/// <summary>
/// p0412: run 1b4b ("update all dependencies", two repos) was cancelled at
/// \$10.13 and 165 model calls, still on phase 1 of 3 — the master lifted
/// packages one at a time, building and re-reading between edits, while the
/// toolchain ships a command that establishes the facts and applies the bulk in
/// one pass. Nothing in its instructions told it to look for that command.
///
/// The two rules are authored in agent-smith-skills; this repo carries them only
/// via <c>SkillsCatalogVersion</c>. So the guard is written against the PACKAGED
/// master and gated on the pin: below <see cref="ToolFirstRelease"/> it states
/// the gap out loud, at or above it the assertions become live automatically —
/// no second commit needed to arm them.
/// </summary>
public sealed class CodingMasterToolFirstPromptTests
{
    /// <summary>First agent-smith-skills release carrying the p0412 rules.</summary>
    private static readonly Version ToolFirstRelease = new(4, 4, 0);

    private const string ToolFirstRule =
        "**Reach for the ecosystem's own tooling before you edit by hand.**";

    private const string VerifyOnceRule =
        "**Verify once per change set, not once per edit.**";

    [Fact]
    public void PackagedCodingMaster_StatesToolFirstAndVerifyOnce()
    {
        var catalog = new EmbeddedSkillsCatalog();
        var pinned = ParsePin(catalog.Version);
        var master = ReadPackagedMaster(catalog, "coding-agent-master");

        if (pinned >= ToolFirstRelease)
        {
            master.Should().Contain(ToolFirstRule,
                "a master told only to do the work hand-edits what a command already knows");
            master.Should().Contain(VerifyOnceRule,
                "a build between every edit buys nothing the build at the end of the set does not");
            return;
        }

        master.Should().NotContain(ToolFirstRule,
            $"the pinned catalog {catalog.Version} predates the p0412 skills release " +
            $"(v{ToolFirstRelease}) — raise SkillsCatalogVersion to it and these assertions go live");
    }

    /// <summary>
    /// The tool establishes the facts and does the bulk; the master judges the
    /// exceptions. Dropping that half turns an expensive correct run into a cheap
    /// wrong one — a blanket bulk run is exactly what gets the few sites the
    /// ticket cares about wrong.
    /// </summary>
    [Fact]
    public void PackagedCodingMaster_KeepsJudgmentWithTheMaster()
    {
        var catalog = new EmbeddedSkillsCatalog();
        if (ParsePin(catalog.Version) < ToolFirstRelease) return;

        var master = ReadPackagedMaster(catalog, "coding-agent-master");

        master.Should().Contain("you judge the exceptions and",
            "tool-first without the exception clause is 'tool instead of model'");
    }

    private static Version ParsePin(string version) =>
        Version.Parse(version.TrimStart('v', 'V'));

    private static string ReadPackagedMaster(EmbeddedSkillsCatalog catalog, string name)
    {
        var path = $"skills/_masters/{name}/SKILL.md";

        using var tarball = catalog.Open();
        using var gz = new GZipStream(tarball, CompressionMode.Decompress);
        using var tar = new TarReader(gz);

        while (tar.GetNextEntry() is { } entry)
        {
            if (!entry.Name.TrimStart('.', '/').Equals(path, StringComparison.Ordinal)) continue;
            if (entry.DataStream is null) continue;

            using var reader = new StreamReader(entry.DataStream);
            return reader.ReadToEnd();
        }

        throw new InvalidOperationException(
            $"'{path}' not found in the embedded skills catalog {catalog.Version} — " +
            "every coding pipeline loads that master at runtime.");
    }
}
