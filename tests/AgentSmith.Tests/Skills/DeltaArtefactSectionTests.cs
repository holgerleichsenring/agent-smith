using AgentSmith.Infrastructure.Core.Services.Skills;
using FluentAssertions;

namespace AgentSmith.Tests.Skills;

/// <summary>
/// 2026-09-20-b5af: the parser's own proof.
/// <para>
/// Until this phase the section had exactly one live positive case — the C# delta's two declared
/// files — and every other case was covered only by "the packaged catalog happens to look like
/// this". With no delta declaring a file, production takes the stated-empty path on every run and
/// the positive path not at all, so the machinery that reads a declared entry is proven here or
/// nowhere. The mechanism stays because a later delta may legitimately declare a file.
/// </para>
/// </summary>
public sealed class DeltaArtefactSectionTests
{
    private const string Preamble = "# Sample Delta\n\n## Additions\n\nA rule.\n\n";

    [Fact]
    public void DeltaArtefactSection_ADeclaredEntry_IsParsedWithItsContent()
    {
        var (delta, artefacts) = DeltaArtefactSection.Split(
            Preamble
            + "## Artefacts\n\n"
            + "### config/sample.toml\n\n"
            + "States the naming rule.\n\n"
            + "```toml\nname = \"sample\"\n```\n");

        artefacts.Should().ContainSingle();
        artefacts[0].Path.Should().Be("config/sample.toml");
        artefacts[0].Content.Should().Be("name = \"sample\"\n");
        delta.Should().NotContain("## Artefacts",
            "the section is the repository's, and the composed principles file must not restate it");
        delta.Should().Contain("## Additions", "only the artefacts section is removed");
    }

    [Fact]
    public void DeltaArtefactSection_TwoEntries_AreBothParsed()
    {
        var (_, artefacts) = DeltaArtefactSection.Split(
            Preamble
            + "## Artefacts\n\n"
            + "### first.toml\n\n```toml\nfirst = 1\n```\n\n"
            + "### second.toml\n\n```toml\nsecond = 2\n```\n");

        artefacts.Should().HaveCount(2);
        artefacts.Select(a => a.Path).Should().Equal("first.toml", "second.toml");
        artefacts.Select(a => a.Content).Should().Equal("first = 1\n", "second = 2\n");
    }

    [Fact]
    public void DeltaArtefactSection_AHeadingWithNoFencedBlock_YieldsNoEntry()
    {
        var (_, artefacts) = DeltaArtefactSection.Split(
            Preamble
            + "## Artefacts\n\n"
            + "### unfinished.toml\n\nSomebody wrote the heading and stopped.\n\n"
            + "### second.toml\n\n```toml\nsecond = 2\n```\n");

        artefacts.Should().ContainSingle(
            "a heading with no fenced block declares nothing, and it must not swallow the next "
            + "entry's content and file it under the wrong path");
        artefacts[0].Path.Should().Be("second.toml");
    }

    [Fact]
    public void DeltaArtefactSection_AnUnterminatedFence_YieldsNoEntry()
    {
        var (_, artefacts) = DeltaArtefactSection.Split(
            Preamble
            + "## Artefacts\n\n"
            + "### truncated.toml\n\n```toml\ntruncated = 1\n");

        artefacts.Should().BeEmpty(
            "a fence nobody closed has no end, so its content is a guess rather than a file");
    }

    [Fact]
    public void DeltaArtefactSection_ASectionStatedEmpty_YieldsNoEntry()
    {
        // This is the path every production run takes now that all three deltas state it.
        var (delta, artefacts) = DeltaArtefactSection.Split(
            Preamble
            + "## Artefacts\n\n"
            + "No artefacts — this delta's rules are enforced by the toolchain the stack\n"
            + "already runs by default, and a file restating them would be a second place\n"
            + "for them to disagree.\n");

        artefacts.Should().BeEmpty("a section stated empty is an answer, not an entry");
        delta.Should().NotContain("No artefacts",
            "the stated-empty prose is removed with the section it heads");
    }
}
