using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Models;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Services;

/// <summary>
/// p0167b: line_range is the canonical PR-line anchor on SkillObservation.
/// Covers the LLM-JSON → typed round-trip through ObservationParser /
/// ObservationNormalizer, the StartLine/EndLine backfill, tolerated shape
/// drift, and the p0151b source-anchor downgrade clearing the range together
/// with the file.
/// </summary>
public sealed class ObservationLineRangeTests
{
    private readonly ObservationParser _parser = TolerantJsonParserFactory.CreateObservation();

    [Fact]
    public void ObservationLineRange_PopulatedFromSkillOutput_RoundTrips()
    {
        const string json = """
            [{"concern": "correctness", "description": "off-by-one in loop bound",
              "severity": "high", "confidence": 90, "blocking": false,
              "file": "src/Foo.cs", "line_range": "12..18"}]
            """;

        var result = _parser.ParseWithoutIds(json, "correctness-reviewer");

        var observation = result.Should().ContainSingle().Which;
        observation.LineRange.Should().Be(new ObservationLineRange(12, 18));
        observation.StartLine.Should().Be(12, "the normalizer backfills StartLine from the range");
        observation.EndLine.Should().Be(18, "the normalizer backfills EndLine from the range");
        observation.LineRange!.ToString().Should().Be("12..18");
    }

    [Fact]
    public void ObservationLineRange_SingleLineForm_ExpandsToDegenerateRange()
    {
        const string json = """
            [{"concern": "correctness", "description": "d", "severity": "low",
              "confidence": 80, "blocking": false, "line_range": "42"}]
            """;

        var result = _parser.ParseWithoutIds(json, "style-reviewer");

        result.Should().ContainSingle().Which.LineRange
            .Should().Be(new ObservationLineRange(42, 42));
    }

    [Fact]
    public void ObservationLineRange_ObjectShapeDrift_Tolerated()
    {
        const string json = """
            [{"concern": "security", "description": "d", "severity": "high",
              "confidence": 85, "blocking": false, "line_range": {"start": 5, "end": 9}}]
            """;

        var result = _parser.ParseWithoutIds(json, "security-overlap-reviewer");

        result.Should().ContainSingle().Which.LineRange
            .Should().Be(new ObservationLineRange(5, 9));
    }

    [Fact]
    public void ObservationLineRange_ReversedSpan_NormalizedSoStartIsLower()
    {
        ObservationLineRange.Parse("18..12").Should().Be(new ObservationLineRange(12, 18));
    }

    [Fact]
    public void ObservationLineRange_Unparseable_DroppedButObservationSurvives()
    {
        const string json = """
            [{"concern": "correctness", "description": "kept", "severity": "low",
              "confidence": 80, "blocking": false, "line_range": "around the middle"}]
            """;

        var result = _parser.ParseWithoutIds(json, "tester");

        var observation = result.Should().ContainSingle().Which;
        observation.Description.Should().Be("kept");
        observation.LineRange.Should().BeNull();
    }

    [Fact]
    public void ObservationLineRange_Absent_StaysNull_AndLegacyLinesUntouched()
    {
        const string json = """
            [{"concern": "correctness", "description": "d", "severity": "low",
              "confidence": 80, "blocking": false, "file": "src/Foo.cs",
              "start_line": 7, "end_line": 9}]
            """;

        var observation = _parser.ParseWithoutIds(json, "tester").Should().ContainSingle().Which;

        observation.LineRange.Should().BeNull("absent means null — backwards compatible");
        observation.StartLine.Should().Be(7);
        observation.EndLine.Should().Be(9);
    }

    [Fact]
    public void ObservationLineRange_ExplicitStartLinePresent_NotOverwrittenByRange()
    {
        const string json = """
            [{"concern": "correctness", "description": "d", "severity": "low",
              "confidence": 80, "blocking": false, "start_line": 3, "line_range": "12..18"}]
            """;

        var observation = _parser.ParseWithoutIds(json, "tester").Should().ContainSingle().Which;

        observation.StartLine.Should().Be(3, "an explicitly emitted start_line wins");
        observation.LineRange.Should().Be(new ObservationLineRange(12, 18));
    }

    [Fact]
    public void SourceAnchorDowngrade_UnreadFileCited_ClearsLineRangeWithFile()
    {
        var validator = new SourceAnchorValidator();
        var observation = new SkillObservation(
            Id: 1, Role: "tester", Concern: ObservationConcern.Correctness,
            Description: "d", Suggestion: "", Blocking: false,
            Severity: ObservationSeverity.Low, Confidence: 80,
            File: "src/NeverRead.cs", EvidenceMode: EvidenceMode.AnalyzedFromSource,
            LineRange: new ObservationLineRange(1, 3));

        var result = validator.EnforceAnchor(
            observation, ["src/SomethingElse.cs"], "tester", NullLogger.Instance);

        result.EvidenceMode.Should().Be(EvidenceMode.Potential);
        result.File.Should().BeNull();
        result.LineRange.Should().BeNull("a line range without its file is a dangling anchor");
    }
}
