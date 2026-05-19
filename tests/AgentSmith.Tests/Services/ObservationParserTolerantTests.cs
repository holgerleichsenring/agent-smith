using AgentSmith.Contracts.Models;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Services;

public sealed class ObservationParserTolerantTests
{
    private readonly AgentSmith.Application.Services.Handlers.ObservationParser _parser =
        TolerantJsonParserFactory.CreateObservation();

    [Fact]
    public void Parse_MixedValidAndInvalidElements_ReturnsValidOnly()
    {
        // Structurally-invalid rows (empty description) are dropped; rows with
        // unknown enum values are kept and defaulted (covered by the normalizer
        // tests).
        const string response = """
            [
              {"concern":"security","description":"valid 1","severity":"high","confidence":80,"blocking":false},
              {"concern":"security","description":"","severity":"high","confidence":80,"blocking":false},
              {"concern":"security","description":"valid 2","severity":"medium","confidence":70,"blocking":false}
            ]
            """;

        var result = _parser.ParseWithoutIds(response, "test-skill", NullLogger.Instance);

        result.Should().HaveCount(2);
        result[0].Description.Should().Be("valid 1");
        result[0].Severity.Should().Be(ObservationSeverity.High);
        result[1].Description.Should().Be("valid 2");
        result[1].Severity.Should().Be(ObservationSeverity.Medium);
    }

    [Fact]
    public void Parse_AllElementsInvalid_FallsBackToSingle()
    {
        const string response = """
            [
              {"concern":"security","description":"","severity":"high","confidence":80,"blocking":false},
              {"concern":"security","description":"","severity":"medium","confidence":70,"blocking":false}
            ]
            """;

        var result = _parser.ParseWithoutIds(response, "test-skill", NullLogger.Instance);

        result.Should().HaveCount(1);
        result[0].Severity.Should().Be(ObservationSeverity.Info);
        result[0].Rationale.Should().Contain("Auto-wrapped");
    }

    [Fact]
    public void Parse_UnknownEnumValues_KeptWithDefaults()
    {
        // Tolerant normalization: severity="warning" is outside the closed set
        // but the row survives — severity falls back to Info, a warning is
        // logged once per (role, field, value).
        const string response = """
            [
              {"concern":"security","description":"row","severity":"warning","confidence":80,"blocking":false}
            ]
            """;

        var result = _parser.ParseWithoutIds(response, "test-skill", NullLogger.Instance);

        result.Should().HaveCount(1);
        result[0].Description.Should().Be("row");
        result[0].Severity.Should().Be(ObservationSeverity.Info);
    }
}
