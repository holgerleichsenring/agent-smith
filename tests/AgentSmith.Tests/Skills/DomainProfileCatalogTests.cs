using System.Formats.Tar;
using System.IO.Compression;
using AgentSmith.Infrastructure.Core.Services.Skills;
using AgentSmith.Tests.Prompts;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Skills;

/// <summary>
/// p0504: a profile is a file in the CATALOG, so which profiles exist is a property of
/// the pin a run resolved — not of the binary, and not of a client project's
/// agentsmith.yml.
/// </summary>
public sealed class DomainProfileCatalogTests : IDisposable
{
    /// <summary>The first catalog release that ships <c>profiles/</c>.</summary>
    private static readonly Version ProfilesFrom = new(4, 7, 0);

    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "agentsmith-p0504-" + Guid.NewGuid().ToString("N"));

    private FileDomainProfileCatalog Catalog() => new(
        new StubSkillsCatalogPath(_root), NullLogger<FileDomainProfileCatalog>.Instance);

    private void WriteProfile(string name, string yaml)
    {
        var directory = Path.Combine(_root, "profiles", name);
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "profile.yaml"), yaml);
    }

    [Fact]
    public void Profile_KnownDomain_ResolvesFromThePinnedCatalog()
    {
        WriteProfile("sample-domain", """
            name: sample-domain
            image: python:3.12-bookworm
            compatible_images:
              - buildpack-deps:bookworm-scm
            verify:
              - stage: parse
                command: tool parse
              - stage: validate
                command: tool validate
            """);

        var profile = Catalog().Find("sample-domain")!;

        profile.Name.Should().Be("sample-domain");
        profile.Image.Should().Be("python:3.12-bookworm");
        profile.CompatibleImages.Should().Equal("buildpack-deps:bookworm-scm");
        profile.Verify.Select(v => v.Command).Should().Equal("tool parse", "tool validate");
    }

    [Fact]
    public void Profile_UnknownDomain_IsNotFound_AndTheKnownOnesAreNameable()
    {
        WriteProfile("sample-domain", "name: sample-domain\nimage: python:3.12-bookworm\nverify: []\n");

        Catalog().Find("not-a-domain").Should().BeNull();
        Catalog().KnownDomains.Should().Equal("sample-domain");
    }

    [Fact]
    public void Profile_MalformedYaml_IsTreatedAsAbsentRatherThanCrashingTheRun()
    {
        WriteProfile("broken", "name: broken\n  image: [unclosed\n");

        Catalog().Find("broken").Should().BeNull();
    }

    /// <summary>
    /// 2026-08-28-3302: no longer armed at the pin. The embedded catalog ships profiles,
    /// so a tarball without them is a defect to name rather than a state to skip over.
    /// </summary>
    [Fact]
    public void Packaging_ProfilesDirectory_IsInTheTarball()
    {
        EntriesInPinnedCatalog()
            .Should().Contain(e => e.StartsWith("profiles/", StringComparison.Ordinal),
                $"the embedded pin is {PackagedMaster.Pin} and profiles ship from "
                + $"{ProfilesFrom} — a profile that does not ship is a domain no run can "
                + "resolve, so a pin below that floor fails here rather than in a run");
    }

    private static IReadOnlyList<string> EntriesInPinnedCatalog()
    {
        using var tarball = PackagedMaster.Catalog.Open();
        using var gz = new GZipStream(tarball, CompressionMode.Decompress);
        using var tar = new TarReader(gz);
        var names = new List<string>();
        while (tar.GetNextEntry() is { } entry) names.Add(entry.Name.TrimStart('.', '/'));
        return names;
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
