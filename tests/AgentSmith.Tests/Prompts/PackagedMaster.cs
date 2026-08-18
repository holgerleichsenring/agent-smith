using System.Formats.Tar;
using System.IO.Compression;
using AgentSmith.Infrastructure.Core.Services.Skills;

namespace AgentSmith.Tests.Prompts;

/// <summary>
/// A master's body as the PINNED CATALOG ships it.
/// <para>
/// The rules these masters carry are authored in agent-smith-skills; this repo holds them
/// only through <c>SkillsCatalogVersion</c>. Reading the packaged tarball is what makes
/// bumping that pin a decision rather than a version string: a release that drops a
/// load-bearing rule fails the build here instead of on a live run.
/// </para>
/// <para>
/// p0442 lifted it out of CodingMasterToolFirstPromptTests, because
/// SpecDerivationPromptTests needed the same read the moment its prompt moved from an
/// embedded resource into the catalog — and a second copy of a tar walk is the way the
/// two drift.
/// </para>
/// </summary>
internal static class PackagedMaster
{
    internal static EmbeddedSkillsCatalog Catalog { get; } = new();

    /// <summary>The pinned release, parsed — for tests that arm themselves at a version.</summary>
    internal static Version Pin => Version.Parse(Catalog.Version.TrimStart('v', 'V'));

    internal static string Read(string name) => Read(Catalog, name);

    internal static string Read(EmbeddedSkillsCatalog catalog, string name)
    {
        ArgumentNullException.ThrowIfNull(catalog);
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
