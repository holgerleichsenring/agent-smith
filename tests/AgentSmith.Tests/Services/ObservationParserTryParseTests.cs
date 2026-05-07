using AgentSmith.Application.Services.Handlers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Services;

public sealed class ObservationParserTryParseTests
{
    [Fact]
    public void TryParseWithoutIds_ValidArray_ReturnsObservations()
    {
        const string response = """
            [
              {"concern":"security","description":"valid 1","severity":"high","confidence":80,"blocking":false},
              {"concern":"security","description":"valid 2","severity":"medium","confidence":70,"blocking":false}
            ]
            """;

        var result = ObservationParser.TryParseWithoutIds(response, "test-skill", NullLogger.Instance);

        result.Should().NotBeNull();
        result!.Should().HaveCount(2);
    }

    [Fact]
    public void TryParseWithoutIds_NotJson_ReturnsNull()
    {
        var result = ObservationParser.TryParseWithoutIds(
            "this is not JSON at all, just prose", "test-skill", NullLogger.Instance);

        result.Should().BeNull();
    }

    [Fact]
    public void TryParseWithoutIds_TruncatedJson_ReturnsNull()
    {
        // Simulates an LLM hitting max_tokens mid-response — opening bracket but no close.
        const string truncated = """[{"concern":"security","description":"valid 1","severity":"high","confidence":80,"blocking":false},{"concern":"security",""";

        var result = ObservationParser.TryParseWithoutIds(truncated, "test-skill", NullLogger.Instance);

        result.Should().BeNull();
    }

    [Fact]
    public void TryParseWithoutIds_EmptyArray_ReturnsNull()
    {
        // Empty array means parser succeeded but found nothing — caller should treat as
        // "no usable observations" same as parse failure for filter purposes.
        var result = ObservationParser.TryParseWithoutIds("[]", "test-skill", NullLogger.Instance);

        result.Should().BeNull();
    }

    [Fact]
    public void TryParseWithoutIds_AllElementsInvalid_ReturnsNull()
    {
        // Every element has a bad enum — none survive element-wise parsing.
        const string response = """
            [
              {"concern":"security","description":"row","severity":"critical","confidence":80,"blocking":false},
              {"concern":"security","description":"row","severity":"warning","confidence":70,"blocking":false}
            ]
            """;

        var result = ObservationParser.TryParseWithoutIds(response, "test-skill", NullLogger.Instance);

        result.Should().BeNull();
    }

    [Fact]
    public void TryParseWithoutIds_MixedValidInvalid_ReturnsValidOnly()
    {
        // Same tolerance as Parse — bad elements skip, good ones survive.
        const string response = """
            [
              {"concern":"security","description":"valid","severity":"high","confidence":80,"blocking":false},
              {"concern":"security","description":"bad","severity":"critical","confidence":80,"blocking":false}
            ]
            """;

        var result = ObservationParser.TryParseWithoutIds(response, "test-skill", NullLogger.Instance);

        result.Should().NotBeNull();
        result!.Should().HaveCount(1);
        result[0].Description.Should().Be("valid");
    }
}
