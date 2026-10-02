using AgentSmith.Application.Services.Specs;
using FluentAssertions;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// 2026-10-02-3f06b: the evidence grammar the record already writes — tolerant of a human's
/// punctuation, never silent about a token that looks like a path.
/// </summary>
public sealed class EvidenceReferencesTests
{
    private readonly EvidenceReferences _reader = new();

    [Fact]
    public void EvidenceReferences_SemicolonAndCommaSpace_SeparatePaths()
    {
        var reading = _reader.Read("src/a.cs:1-3; src/b.cs:4, src/c.cs");

        reading.References.Select(r => r.Path).Should().Equal("src/a.cs", "src/b.cs", "src/c.cs");
        reading.References[2].LineText.Should().BeNull();
    }

    [Fact]
    public void EvidenceReferences_ContinuationAfterComma_BindsToThePreviousPath()
    {
        var reading = _reader.Read("src/a.cs:1-3, :47");

        reading.References.Should().HaveCount(2);
        reading.References[1].Path.Should().Be("src/a.cs");
        reading.References[1].Lines.Should().Equal(new EvidenceLineRange(47, 47));
    }

    [Fact]
    public void EvidenceReferences_InTokenLineList_ReadsEveryLine()
    {
        var reference = _reader.Read("src/a.cs:35,103-113").References.Should().ContainSingle().Subject;

        reference.Lines.Should().Equal(new EvidenceLineRange(35, 35), new EvidenceLineRange(103, 113));
    }

    [Fact]
    public void EvidenceReferences_TrailingPunctuationAndParens_AreStripped()
    {
        var reading = _reader.Read("(`src/a.cs:12`), \"src/b.cs\".");

        reading.References.Select(r => (r.Path, r.LineText)).Should().Equal(("src/a.cs", "12"), ("src/b.cs", null));
    }

    [Fact]
    public void EvidenceReferences_RootFileWithLines_IsAPath()
    {
        var reading = _reader.Read("CLAUDE.md:12-14 and DESIGN.md alone");

        reading.References.Should().ContainSingle().Which.Path.Should().Be("CLAUDE.md",
            "a root file is a path only with lines; without them it reads as a word");
    }

    [Fact]
    public void EvidenceReferences_Url_IsNotAPath() =>
        _reader.Read("https://example.test/docs/page.md:3").References.Should().BeEmpty();

    [Fact]
    public void EvidenceReferences_ObservedSegmentAfterAPath_IsNotResolved()
    {
        var reading = _reader.Read("src/a.cs:1; observed: the run log under logs/run.txt, 2026-10-02");

        reading.References.Should().ContainSingle().Which.Path.Should().Be("src/a.cs");
        reading.Observations.Should().ContainSingle().Which.Should().StartWith("observed:");
    }

    [Fact]
    public void EvidenceReferences_QualifiedPath_KeepsItsQualifier()
    {
        var reference = _reader.Read("agent-smith-skills:skills/x/SKILL.md:4").References.Should().ContainSingle().Subject;

        (reference.Qualifier, reference.Path, reference.LineText).Should().Be(("agent-smith-skills", "skills/x/SKILL.md", "4"));
    }

    [Fact]
    public void EvidenceReferences_MintedSegment_IsCountedNotResolved()
    {
        var reading = _reader.Read("[L3] api: the derivation ran 'ls src/x' exited 0");

        reading.Minted.Should().ContainSingle();
        reading.References.Should().BeEmpty();
    }
}
