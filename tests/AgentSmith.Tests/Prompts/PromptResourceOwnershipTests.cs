using System.Formats.Tar;
using System.IO.Compression;
using AgentSmith.Infrastructure.Core.Services.Skills;
using AgentSmith.Tests.Architecture;
using FluentAssertions;

namespace AgentSmith.Tests.Prompts;

/// <summary>
/// p0415: the two guards that keep <c>Prompts/Resources</c> honest.
/// <para>
/// A prompt name with two bodies is a coin toss the operator cannot see: two live
/// runs served the CATALOG copy of spec-derivation-master — the one without the
/// ships_code obligation the parser enforces — because a direct-name match ran
/// before the p0205 guard. Ownership is declared now, but a duplicate is still a
/// trap for the next reader, so it fails the build.
/// </para>
/// <para>
/// Named gap: this reaches the EMBEDDED skills pin only. An operator may point
/// agentsmith.yml at any catalog directory, which the repo cannot see at build
/// time; runtime determinism there comes from the ownership table, not from here.
/// Making an operator's duplicate a startup failure would break deployments that
/// work today.
/// </para>
/// </summary>
public sealed class PromptResourceOwnershipTests
{
    [Fact]
    public void NoPromptName_HasTwoOwners()
    {
        var masters = EmbeddedCatalogMasterNames();
        masters.Should().NotBeEmpty("the pinned catalog must ship masters at all");

        var duplicates = EmbeddedPromptNames()
            .Where(masters.Contains)
            .Select(name =>
                $"'{name}' is served by BOTH {ResourcesDirectory()}/{name}.md AND the "
                + $"embedded skills catalog's skills/_masters/{name}/SKILL.md")
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        duplicates.Should().BeEmpty(
            "a prompt name resolves from exactly ONE owner. Delete the embedded resource "
            + "and declare the name catalog-owned in PromptOwnership, or keep it embedded "
            + "and drop the master from the catalog.\n  "
            + string.Join("\n  ", duplicates));
    }

    [Fact]
    public void EveryEmbeddedPrompt_IsRequestedFromSource()
    {
        var sources = ArchitectureSources.HandWrittenBackendFiles()
            .Select(File.ReadAllText)
            .ToList();

        var unrequested = EmbeddedPromptNames()
            .Where(name => !sources.Any(s => s.Contains($"\"{name}\"", StringComparison.Ordinal)))
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        unrequested.Should().BeEmpty(
            "a prompt no code asks for is dead weight that reads as a live contract. "
            + "Delete the resource together with whatever loaded it.\n  "
            + string.Join("\n  ", unrequested));
    }

    private static IReadOnlyList<string> EmbeddedPromptNames() =>
        [.. Directory.EnumerateFiles(ResourcesDirectory(), "*.md")
            .Select(Path.GetFileNameWithoutExtension)
            .Select(n => n!)];

    private static string ResourcesDirectory() => Path.Combine(
        ArchitectureSources.BackendRoot, "AgentSmith.Application", "Prompts", "Resources");

    /// <summary>Master-skill names in the tarball this build embeds — read straight
    /// out of the gzip stream, so the pin under test is the shipped one.</summary>
    private static HashSet<string> EmbeddedCatalogMasterNames()
    {
        using var stream = new EmbeddedSkillsCatalog().Open();
        using var gz = new GZipStream(stream, CompressionMode.Decompress);
        using var tar = new TarReader(gz);
        var names = new HashSet<string>(StringComparer.Ordinal);
        while (tar.GetNextEntry() is { } entry)
        {
            var parts = entry.Name.Replace('\\', '/').Split('/');
            var mastersIndex = Array.IndexOf(parts, "_masters");
            if (mastersIndex >= 0 && mastersIndex + 1 < parts.Length)
                names.Add(parts[mastersIndex + 1]);
        }
        return names;
    }
}
