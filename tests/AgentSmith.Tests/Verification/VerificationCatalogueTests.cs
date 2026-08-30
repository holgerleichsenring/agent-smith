using System.Security.Cryptography;
using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Core.Services.Verification;
using AgentSmith.Tests.Architecture;
using FluentAssertions;

namespace AgentSmith.Tests.Verification;

/// <summary>
/// 2026-08-30-0ea8: the questions a scan asks are entries of a published standard, from a
/// checked-in copy, with ids the product did not invent. These rules hold the four claims
/// that makes: the text is verbatim, the copy is the pinned one, the lens classifies all
/// of it while reproducing none of it, and a run that consults it says which release
/// answered.
/// </summary>
public sealed class VerificationCatalogueTests
{
    private static AsvsVerificationLens Lens(IReadOnlyList<VerificationRequirement>? entries = null) =>
        new(entries is null
                ? new EmbeddedVerificationCatalogue(new AsvsFlatExportParser())
                : new StubVerificationCatalogue(AsvsRelease.Tag, entries),
            new VerificationLensTableParser());

    [Fact]
    public void Catalogue_AnIngestedEntry_KeepsItsIdLevelAndText()
    {
        var catalogue = new EmbeddedVerificationCatalogue(new AsvsFlatExportParser());

        var ingested = catalogue.Requirements;

        ingested.Should().BeEquivalentTo(CheckedInVerificationFiles.ExportedRequirements(),
            options => options.WithStrictOrdering(),
            "an entry is ingested verbatim — a citation of an edited clause cites nothing");
        ingested.Should().HaveCount(345);
        ingested.Select(r => r.Level).Should().OnlyContain(level => level == "1" || level == "2" || level == "3",
            "the level is the standard's own value, as the export writes it");
    }

    [Fact]
    public void Catalogue_ACheckedInCopyWhoseDigestDiffers_FailsTheBuild()
    {
        using var file = File.OpenRead(CheckedInVerificationFiles.ExportPath);
        var digest = Convert.ToHexString(SHA256.HashData(file)).ToLowerInvariant();

        CheckedInVerificationFiles.ProjectProperty("VerificationCatalogueSha256").Should().Be(digest,
            "the build compares this pin against the file and errors on a mismatch, so the "
            + "copy that ships is the release that was reviewed");
        File.ReadAllText(CheckedInVerificationFiles.ProjectPath).Should()
            .Contain("VerificationCatalogueActualSha256.ToLowerInvariant()")
            .And.Contain("Verification catalogue SHA256 mismatch",
                "the comparison is the build's, not this test's");
    }

    [Fact]
    public void Catalogue_AnIdTheLensDoesNotClassify_FailsTheBuild()
    {
        var unknown = new VerificationRequirement("V99.9.9", "1", "An entry the table never saw.");

        var refusal = () => Lens([unknown]);

        refusal.Should().Throw<InvalidOperationException>().WithMessage("*V99.9.9*");
        Lens().Should().NotBeNull("every id the checked-in export carries is classified");
        CheckedInVerificationFiles.ProjectProperty("VerificationRequirementCount").Should()
            .Be($"{new EmbeddedVerificationCatalogue(new AsvsFlatExportParser()).Requirements.Count}",
                "the build counts the table's rows against this number before the lens ever loads");
    }

    [Fact]
    public void Catalogue_TheLensTable_HoldsNoRequirementText()
    {
        var table = File.ReadAllText(CheckedInVerificationFiles.LensPath);
        var stations = Enum.GetNames<VerificationStation>().Select(n => n.ToLowerInvariant()).ToHashSet();

        var rows = File.ReadAllLines(CheckedInVerificationFiles.LensPath)
            .Where(line => line.Length > 0 && line[0] != '#')
            .Select(line => line.Split('\t'))
            .ToList();

        rows.Should().OnlyContain(
            columns => columns.Length == 2 && columns[1].Split(',').All(s => s == "none" || stations.Contains(s)),
            "the table holds ids and classifications — a table reproducing the standard's text "
            + "would be adapted material and would put the ShareAlike term on the product");
        CheckedInVerificationFiles.ExportedRequirements()
            .Should().OnlyContain(requirement => !table.Contains(requirement.Text, StringComparison.Ordinal));
    }

    [Theory]
    [MemberData(nameof(Stations))]
    public void Catalogue_ASingleStationSelection_StaysWithinItsBound(VerificationStation station)
    {
        var selection = Lens().For(new PipelineContext(), station);

        selection.Requirements.Should().HaveCountLessThanOrEqualTo(AsvsVerificationLens.MaxEntriesPerStation,
            "a station is asked what one agent can answer; thousands of questions is the "
            + "unanswerable-question failure this bound exists to prevent");
        selection.Requirements.Select(r => r.Level).Should().OnlyContain(level => level == "1" || level == "2",
            "the default floor is the first two levels");
        selection.Requirements.Should().NotBeEmpty();
    }

    [Fact]
    public void Catalogue_ARunThatConsultedIt_RecordsItsVersion()
    {
        var run = new PipelineContext();

        var selection = Lens().For(run, VerificationStation.Authority);

        run.TryGet<string>(ContextKeys.VerificationCatalogueVersion, out var recorded).Should().BeTrue(
            "5.0 renumbered the whole standard, so an id cited without its version cites nothing");
        recorded.Should().Be(AsvsRelease.Tag);
        selection.CatalogueVersion.Should().Be(AsvsRelease.Tag);
        selection.Attribution.Should().Contain("CC BY-SA 4.0").And.Contain("OWASP",
            "the licence line travels with the text wherever it is quoted");
    }

    [Fact]
    public void Catalogue_TheReleaseItCameFrom_IsNamedInOnePlace()
    {
        var naming = Sources()
            .Where(path => File.ReadAllText(path).Contains(AsvsRelease.Tag, StringComparison.Ordinal))
            .Select(path => Path.GetFileName(path))
            .ToList();

        naming.Should().Equal([$"{nameof(AsvsRelease)}.cs"],
            "one constant, so a later phase turns the source into a port with a rename "
            + "rather than a refactor");
    }

    public static TheoryData<VerificationStation> Stations()
    {
        var stations = new TheoryData<VerificationStation>();
        foreach (var station in Enum.GetValues<VerificationStation>()) stations.Add(station);
        return stations;
    }

    // Everything this repository compiles into the product: the release may be named in
    // exactly one of these files. NOTICE and the phase record are prose for people.
    private static IEnumerable<string> Sources() =>
        Directory.EnumerateFiles(ArchitectureSources.SourceRoot, "*.*", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".cs", StringComparison.Ordinal)
                           || path.EndsWith(".csproj", StringComparison.Ordinal))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                               StringComparison.Ordinal)
                           && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
                               StringComparison.Ordinal));
}
