using AgentSmith.Tests.TestHelpers;
using FluentAssertions;

namespace AgentSmith.Tests.Services;

public class ObservationParserTests
{
    private readonly AgentSmith.Application.Services.Handlers.ObservationParser _parser =
        TolerantJsonParserFactory.CreateObservation();

    [Fact]
    public void ParseWithoutIds_ValidJson_ReturnsObservationsWithIdZero()
    {
        const string json = """
            [
              {"concern": "Correctness", "description": "first", "severity": "Medium", "confidence": 80, "blocking": false},
              {"concern": "Security", "description": "second", "severity": "High", "confidence": 90, "blocking": true}
            ]
            """;

        var result = _parser.ParseWithoutIds(json, "tester");

        result.Should().HaveCount(2);
        result.Should().AllSatisfy(o => o.Id.Should().Be(0));
    }

    [Fact]
    public void ParseWithoutIds_FallbackPath_AlsoUsesIdZero()
    {
        var result = _parser.ParseWithoutIds("not json", "tester");

        result.Should().HaveCount(1);
        result[0].Id.Should().Be(0);
    }
}
