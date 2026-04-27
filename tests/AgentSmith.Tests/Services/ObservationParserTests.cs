using AgentSmith.Application.Services.Handlers;
using FluentAssertions;

namespace AgentSmith.Tests.Services;

public class ObservationParserTests
{
    [Fact]
    public void ParseWithoutIds_ValidJson_ReturnsObservationsWithIdZero()
    {
        const string json = """
            [
              {"concern": "Correctness", "description": "first", "severity": "Medium", "confidence": 80, "blocking": false},
              {"concern": "Security", "description": "second", "severity": "High", "confidence": 90, "blocking": true}
            ]
            """;

        var result = ObservationParser.ParseWithoutIds(json, "tester");

        result.Should().HaveCount(2);
        result.Should().AllSatisfy(o => o.Id.Should().Be(0));
    }

    [Fact]
    public void ParseWithoutIds_FallbackPath_AlsoUsesIdZero()
    {
        var result = ObservationParser.ParseWithoutIds("not json", "tester");

        result.Should().HaveCount(1);
        result[0].Id.Should().Be(0);
    }
}
