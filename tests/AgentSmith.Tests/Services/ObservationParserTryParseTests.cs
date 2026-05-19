using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Services;

public sealed class ObservationParserTryParseTests
{
    private readonly AgentSmith.Application.Services.Handlers.ObservationParser _parser =
        TolerantJsonParserFactory.CreateObservation();

    [Fact]
    public void TryParseWithoutIds_ValidArray_ReturnsObservations()
    {
        const string response = """
            [
              {"concern":"security","description":"valid 1","severity":"high","confidence":80,"blocking":false},
              {"concern":"security","description":"valid 2","severity":"medium","confidence":70,"blocking":false}
            ]
            """;

        var result = _parser.TryParseWithoutIds(response, "test-skill", NullLogger.Instance);

        result.Should().NotBeNull();
        result!.Should().HaveCount(2);
    }

    [Fact]
    public void TryParseWithoutIds_NotJson_ReturnsNull()
    {
        var result = _parser.TryParseWithoutIds(
            "this is not JSON at all, just prose", "test-skill", NullLogger.Instance);

        result.Should().BeNull();
    }

    [Fact]
    public void TryParseWithoutIds_TruncatedJson_RecoversCompleteObjects()
    {
        // Simulates an LLM hitting max_tokens mid-response — opening bracket but no close.
        // Tolerant parser recovers complete object literals from the truncated array via
        // brace-counting. The first object is complete and should be recovered; the
        // trailing partial object is dropped.
        const string truncated = """[{"concern":"security","description":"valid 1","severity":"high","confidence":80,"blocking":false},{"concern":"security",""";

        var result = _parser.TryParseWithoutIds(truncated, "test-skill", NullLogger.Instance);

        result.Should().NotBeNull();
        result!.Should().HaveCount(1);
        result[0].Description.Should().Be("valid 1");
    }

    [Fact]
    public void TryParseWithoutIds_EmptyArray_ReturnsNull()
    {
        var result = _parser.TryParseWithoutIds("[]", "test-skill", NullLogger.Instance);

        result.Should().BeNull();
    }

    [Fact]
    public void TryParseWithoutIds_AllElementsInvalid_ReturnsNull()
    {
        const string response = """
            [
              {"concern":"security","description":"row","severity":"blocker","confidence":80,"blocking":false},
              {"concern":"security","description":"row","severity":"warning","confidence":70,"blocking":false}
            ]
            """;

        var result = _parser.TryParseWithoutIds(response, "test-skill", NullLogger.Instance);

        result.Should().BeNull();
    }

    [Fact]
    public void TryParseWithoutIds_MixedValidInvalid_ReturnsValidOnly()
    {
        const string response = """
            [
              {"concern":"security","description":"valid","severity":"high","confidence":80,"blocking":false},
              {"concern":"security","description":"bad","severity":"blocker","confidence":80,"blocking":false}
            ]
            """;

        var result = _parser.TryParseWithoutIds(response, "test-skill", NullLogger.Instance);

        result.Should().NotBeNull();
        result!.Should().HaveCount(1);
        result[0].Description.Should().Be("valid");
    }
}
