using System.Text.Json;
using AgentSmith.Application.Services.Json;
using FluentAssertions;

namespace AgentSmith.Tests.Json;

/// <summary>
/// p0426: the producer of this JSON is a language model. Every field is optional, every
/// type is a suggestion, and a parser that trusts either ends the run — which is exactly
/// what happened at step 12 of run 27, on an analyzer that answered
/// <c>file_count: null</c>.
/// </summary>
public sealed class JsonValueReaderTests
{
    [Fact]
    public void ANullWhereANumberWasExpected_ReadsTheFallback_NotAnException()
    {
        var element = Parse("""{"file_count": null}""");

        JsonValueReader.Int32(element, "file_count", fallback: 7).Should().Be(7);
    }

    [Fact]
    public void AStringWhereANumberWasExpected_ReadsTheFallback()
    {
        var element = Parse("""{"confidence": "high", "line": "42"}""");

        JsonValueReader.Int32(element, "line", fallback: 0).Should().Be(0);
        JsonValueReader.Double(element, "confidence").Should().Be(0);
    }

    [Fact]
    public void AnAbsentProperty_ReadsTheFallback()
        => JsonValueReader.Int32(Parse("{}"), "confidence", fallback: 80).Should().Be(80);

    [Fact]
    public void ARealNumber_IsStillRead()
    {
        var element = Parse("""{"file_count": 12, "score": 0.75, "ok": true, "name": "x"}""");

        JsonValueReader.Int32(element, "file_count").Should().Be(12);
        JsonValueReader.Double(element, "score").Should().Be(0.75);
        JsonValueReader.Bool(element, "ok").Should().BeTrue();
        JsonValueReader.Text(element, "name").Should().Be("x");
    }

    /// <summary>
    /// The trap this exists for: <c>TryGetInt32</c> reads like a safe accessor and throws
    /// on any kind that is not Number. It returns false only for a number that does not
    /// fit — which is not the case anyone was defending against.
    /// </summary>
    [Fact]
    public void TheRawAccessor_ThrowsWhereThisOneDoesNot()
    {
        var value = Parse("""{"file_count": null}""").GetProperty("file_count");

        var raw = () => value.TryGetInt32(out _);

        raw.Should().Throw<InvalidOperationException>();
        JsonValueReader.Int32(Parse("""{"file_count": null}"""), "file_count").Should().Be(0);
    }

    [Fact]
    public void ABareNumberInAnArray_IsReadByKind()
    {
        var array = Parse("""[3, null, "x"]""");
        var values = array.EnumerateArray().Select(e => JsonValueReader.Int32(e, -1)).ToList();

        values.Should().Equal(3, -1, -1);
    }

    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement.Clone();
}
