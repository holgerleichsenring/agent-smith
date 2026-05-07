using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Services;

public sealed class ObservationParserTolerantTests
{
    [Fact]
    public void Parse_MixedValidAndInvalidElements_ReturnsValidOnly()
    {
        const string response = """
            [
              {"concern":"security","description":"valid 1","severity":"high","confidence":80,"blocking":false},
              {"concern":"security","description":"bad row","severity":"critical","confidence":80,"blocking":false},
              {"concern":"security","description":"valid 2","severity":"medium","confidence":70,"blocking":false}
            ]
            """;

        var result = ObservationParser.ParseWithoutIds(response, "test-skill", NullLogger.Instance);

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
              {"concern":"security","description":"row 1","severity":"critical","confidence":80,"blocking":false},
              {"concern":"security","description":"row 2","severity":"warning","confidence":70,"blocking":false}
            ]
            """;

        var result = ObservationParser.ParseWithoutIds(response, "test-skill", NullLogger.Instance);

        result.Should().HaveCount(1);
        result[0].Severity.Should().Be(ObservationSeverity.Info);
        result[0].Rationale.Should().Contain("Auto-wrapped");
    }

    [Fact]
    public void Parse_SingleInvalidElement_DoesNotThrow()
    {
        const string response = """
            [
              {"concern":"security","description":"bad","severity":"critical","confidence":80,"blocking":false}
            ]
            """;

        var act = () => ObservationParser.ParseWithoutIds(response, "test-skill", NullLogger.Instance);

        act.Should().NotThrow();
    }
}
