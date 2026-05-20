using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Services;

/// <summary>
/// Covers the empty-response branch of <see cref="ObservationRecoveryHelper.FallbackSingle"/>.
/// When a skill returns nothing parseable AND nothing renderable, the fallback used to emit a
/// SkillObservation with empty Description / Confidence=50 — surfaced to operators as a ghost
/// INFO finding. The fix emits a meta-observation with Category=execution-parse-failure so the
/// output strategies filter it out of the severity tally and into the limits footer.
/// </summary>
public sealed class ObservationParserEmptyResponseTests
{
    private readonly ObservationParser _parser;

    public ObservationParserEmptyResponseTests()
    {
        var tolerantParser = new Mock<ITolerantJsonParser>();
        tolerantParser.Setup(p => p.ParseArray(It.IsAny<string>()))
            .Returns(new TolerantParseResult(null, Array.Empty<TolerantParseDiagnostic>()));
        tolerantParser.Setup(p => p.ExtractArrayObjects(It.IsAny<string>())).Returns(Array.Empty<string>());
        var normalizer = new Mock<IObservationNormalizer>();
        var anchorValidator = new Mock<ISourceAnchorValidator>();
        anchorValidator
            .Setup(v => v.EnforceAnchor(It.IsAny<SkillObservation>(), It.IsAny<IReadOnlyCollection<string>?>(), It.IsAny<string>(), It.IsAny<Microsoft.Extensions.Logging.ILogger?>()))
            .Returns<SkillObservation, IReadOnlyCollection<string>?, string, Microsoft.Extensions.Logging.ILogger?>((o, _, _, _) => o);
        _parser = new ObservationParser(tolerantParser.Object, normalizer.Object, anchorValidator.Object);
    }

    [Fact]
    public void Parse_EmptyResponse_EmitsMetaObservation_NotEmptyPlaceholder()
    {
        var result = _parser.Parse(string.Empty, "report-synthesizer", 1, NullLogger.Instance);

        result.Should().HaveCount(1);
        result[0].Category.Should().Be(ExecutionLimitCategories.ExecutionParseFailure);
        result[0].Description.Should().Contain("report-synthesizer");
        result[0].Description.Should().Contain("empty response");
        result[0].Confidence.Should().Be(0);
        result[0].Severity.Should().Be(ObservationSeverity.Info);
    }

    [Fact]
    public void Parse_WhitespaceOnlyResponse_EmitsMetaObservation()
    {
        var result = _parser.Parse("   \n\t  ", "jwt-validation-judge", 1, NullLogger.Instance);

        result.Should().HaveCount(1);
        result[0].Category.Should().Be(ExecutionLimitCategories.ExecutionParseFailure);
        result[0].Description.Should().Contain("jwt-validation-judge");
    }

    [Fact]
    public void Parse_NonEmptyUnparseableResponse_PreservesWrappingBehaviour()
    {
        var result = _parser.Parse("This is not JSON, just prose.", "architect", 1, NullLogger.Instance);

        result.Should().HaveCount(1);
        result[0].Category.Should().BeNull();
        result[0].Description.Should().Contain("This is not JSON");
        result[0].Confidence.Should().Be(50);
    }
}
