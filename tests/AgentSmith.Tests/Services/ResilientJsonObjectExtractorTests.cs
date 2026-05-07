using AgentSmith.Application.Services.Handlers;
using FluentAssertions;

namespace AgentSmith.Tests.Services;

public sealed class ResilientJsonObjectExtractorTests
{
    [Fact]
    public void ExtractObjects_CompleteArray_ReturnsAllObjects()
    {
        const string input = """[{"a":1},{"b":2},{"c":3}]""";

        var result = ResilientJsonObjectExtractor.ExtractObjects(input).ToList();

        result.Should().HaveCount(3);
        result[0].Should().Be("""{"a":1}""");
        result[1].Should().Be("""{"b":2}""");
        result[2].Should().Be("""{"c":3}""");
    }

    [Fact]
    public void ExtractObjects_TruncatedAfterSecondObject_ReturnsTwoObjects()
    {
        // Third object's closing brace is missing; trailing partial discarded.
        const string input = """[{"a":1},{"b":2},{"c":""";

        var result = ResilientJsonObjectExtractor.ExtractObjects(input).ToList();

        result.Should().HaveCount(2);
        result[0].Should().Be("""{"a":1}""");
        result[1].Should().Be("""{"b":2}""");
    }

    [Fact]
    public void ExtractObjects_TruncatedMidString_ReturnsCompleteObjectsBeforeIt()
    {
        // String value contains an unescaped `}` that should NOT close the object.
        // Then the response is truncated mid-string.
        const string input = """[{"description":"this } is inside a string"},{"description":"another"},{"description":"trunc""";

        var result = ResilientJsonObjectExtractor.ExtractObjects(input).ToList();

        result.Should().HaveCount(2);
        result[0].Should().Contain("this } is inside");
        result[1].Should().Be("""{"description":"another"}""");
    }

    [Fact]
    public void ExtractObjects_EscapeSequencesAndNestedObjects_HandlesCorrectly()
    {
        // Nested object inside property value, plus escaped quote in string.
        const string input = """[{"meta":{"nested":true},"description":"has \"quotes\""}]""";

        var result = ResilientJsonObjectExtractor.ExtractObjects(input).ToList();

        result.Should().HaveCount(1);
        result[0].Should().Contain("nested").And.Contain("\\\"quotes\\\"");
    }

    [Fact]
    public void ExtractObjects_NoOpenBracket_ReturnsEmpty()
    {
        // Pure prose, no JSON object literals at all.
        const string input = "Sorry, I cannot filter these observations as JSON.";

        var result = ResilientJsonObjectExtractor.ExtractObjects(input).ToList();

        result.Should().BeEmpty();
    }
}
