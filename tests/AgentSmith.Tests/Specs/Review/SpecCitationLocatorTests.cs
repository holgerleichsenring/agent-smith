using AgentSmith.Application.Services.Specs;
using AgentSmith.Application.Services.Specs.Review;
using AgentSmith.Contracts.Specs;
using FluentAssertions;

namespace AgentSmith.Tests.Specs.Review;

/// <summary>2026-10-13-8762n: where a cited block stands now — exact text, unique match, ranges keep their length.</summary>
public sealed class SpecCitationLocatorTests
{
    private const string Old = "namespace Shop;\npublic static class OrderRules\n{\n    public const int MaxLines = 40;\n    public const int MinLines = 1;\n}\n";

    private static IReadOnlyList<IReadOnlyList<string>> Blocks(string text, params EvidenceLineRange[] ranges) =>
        [.. ranges.Select(r => SpecCitationLocator.Block(SpecCitationLocator.Lines(text), r)!)];

    [Fact]
    public void SpecCitationLocator_LinesUnchanged_Holds()
    {
        var range = new EvidenceLineRange(4, 5);

        SpecCitationLocator.InFile(Blocks(Old, range), [range], Old.Replace("40;", "40;   ")).Should().BeNull(
            "the lines still read the same, trailing whitespace aside");
    }

    [Fact]
    public void SpecCitationLocator_BlockMovedDownInFile_NewRangeSameLength()
    {
        var range = new EvidenceLineRange(4, 5);
        var current = "// header\n// more\n" + Old;

        SpecCitationLocator.InFile(Blocks(Old, range), [range], current).Should().Equal(new EvidenceLineRange(6, 7));
    }

    [Fact]
    public void SpecCitationLocator_BlockFoundTwice_NotFound()
    {
        var range = new EvidenceLineRange(4, 5);
        var current = "// header\n" + Old + Old;

        SpecCitationLocator.InFile(Blocks(Old, range), [range], current).Should().BeEmpty("a block found twice moves nowhere");
    }

    [Fact]
    public void SpecCitationLocator_BlockInOneOtherFile_NewPath()
    {
        var range = new EvidenceLineRange(4, 5);
        var blocks = Blocks(Old, range);

        var hit = SpecCitationLocator.InOne(blocks, [("src/Other.cs", "class Other {}\n"), ("src/Billing.cs", "// moved\n" + Old)]);

        hit.Should().NotBeNull();
        hit!.Value.Path.Should().Be("src/Billing.cs");
        hit.Value.Ranges.Should().Equal(new EvidenceLineRange(5, 6));
        SpecCitationLocator.InOne(blocks, [("a.cs", Old), ("b.cs", Old)]).Should().BeNull("two candidates hold it");
    }

    [Fact]
    public void SpecCitationLocator_BuildNoteMovedDestination_IsTriedBeforeGrep()
    {
        PrecedingNote[] notes =
        [
            new("2026-10-14-1111a", "done/a.yaml",
                new SpecBuildNote("2026-10-14-1111a", [], ["src/Orders.cs -> src/Rules/OrderRules.cs", "x.cs -> y.cs"], [], [])),
        ];

        var destinations = SpecCitationLocator.NoteDestinations(notes, "src/Orders.cs");
        var candidates = SpecCitationLocator.Candidates(destinations, ["src/Grep.cs", "src/Rules/OrderRules.cs", "src/Orders.cs"], "src/Orders.cs");

        candidates.Should().Equal("src/Rules/OrderRules.cs", "src/Grep.cs");
        SpecCitationLocator.Candidates([], Enumerable.Range(1, 11).Select(i => $"f{i}.cs"), "src/Orders.cs")
            .Should().BeNull("more than ten candidate files is not found");
    }

    [Fact]
    public void SpecCitationLocator_ReindentedLine_NotFound()
    {
        var range = new EvidenceLineRange(4, 5);
        var current = "// header\n" + Old.Replace("    public const int MaxLines", "        public const int MaxLines");

        SpecCitationLocator.InFile(Blocks(Old, range), [range], current).Should().BeEmpty("leading whitespace is text");
    }

    [Fact]
    public void SpecCitationLocator_SearchLine_BracesOnlyBlock_HasNone()
    {
        SpecCitationLocator.SearchLine([["{", "    }", "return;"]]).Should().BeNull();
        SpecCitationLocator.SearchLine(Blocks(Old, new EvidenceLineRange(2, 5)))
            .Should().Be("public const int MaxLines = 40;");
    }
}
