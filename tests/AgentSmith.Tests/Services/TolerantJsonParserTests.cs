using System.Text.Json;
using AgentSmith.Contracts.Models;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;

namespace AgentSmith.Tests.Services;

/// <summary>
/// p0147a: covers the six known LLM JSON failure shapes the consolidated
/// tolerant parser is responsible for: plain JSON, ```json-fenced JSON,
/// truncated array, verbose prose wrapper, empty-string field, malformed
/// entry mixed with valid.
/// </summary>
public sealed class TolerantJsonParserTests
{
    private readonly AgentSmith.Application.Services.TolerantJsonParser _parser =
        TolerantJsonParserFactory.CreateTolerant();

    [Fact]
    public void ParseObject_PlainJson_ReturnsDocument_NoDiagnostics()
    {
        var raw = """{"a":1,"b":"two"}""";

        var result = _parser.ParseObject(raw);

        result.Document.Should().NotBeNull();
        result.Document!.RootElement.GetProperty("a").GetInt32().Should().Be(1);
        result.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void ParseObject_JsonFenced_StripsFence_LeavesValidDocument()
    {
        var raw = """
            ```json
            {"a":1}
            ```
            """;

        var result = _parser.ParseObject(raw);

        result.Document.Should().NotBeNull();
        result.Document!.RootElement.GetProperty("a").GetInt32().Should().Be(1);
        result.Diagnostics.Should().ContainSingle(d => d.Kind == TolerantRecoveryKind.FencesStripped);
    }

    [Fact]
    public void ParseObject_VerboseProseWrapper_ExtractsObjectSpan()
    {
        var raw = "Sure thing! Here is the JSON you asked for: " +
                  """{"verdict":"pass","reason":"all good"}""" +
                  " — let me know if you need more.";

        var result = _parser.ParseObject(raw);

        result.Document.Should().NotBeNull();
        result.Document!.RootElement.GetProperty("verdict").GetString().Should().Be("pass");
        result.Diagnostics.Should().Contain(d => d.Kind == TolerantRecoveryKind.JsonExtracted);
    }

    [Fact]
    public void ExtractArrayObjects_TruncatedMidArray_RecoversCompleteObjects()
    {
        const string truncated = """[{"a":1},{"b":2},{"c":""";

        var literals = _parser.ExtractArrayObjects(truncated);

        literals.Should().HaveCount(2);
        literals[0].Should().Be("""{"a":1}""");
        literals[1].Should().Be("""{"b":2}""");
    }

    [Fact]
    public void GetStringOrNull_EmptyStringField_ReturnsNull()
    {
        var raw = """{"category":"","file":"src/Foo.cs"}""";
        using var doc = JsonDocument.Parse(raw);

        _parser.GetStringOrNull(doc.RootElement, "category").Should().BeNull();
        _parser.GetStringOrNull(doc.RootElement, "missing").Should().BeNull();
        _parser.GetStringOrNull(doc.RootElement, "file").Should().Be("src/Foo.cs");
    }

    [Fact]
    public void ExtractArrayObjects_MalformedEntryMixedWithValid_RecoversValidObjectsOnly()
    {
        // Outer-level brace-counting is string-literal-aware. The first object has
        // an unescaped `}` *inside a string value* which must NOT close the object.
        // The third object is truncated mid-content; it is silently dropped.
        const string raw = """[{"description":"this } is inside a string"},{"description":"another"},{"description":"trunc""";

        var literals = _parser.ExtractArrayObjects(raw);

        literals.Should().HaveCount(2);
        literals[0].Should().Contain("this } is inside");
        literals[1].Should().Be("""{"description":"another"}""");
    }

    [Fact]
    public void ParseObject_Empty_ReportsFailedDiagnostic()
    {
        var result = _parser.ParseObject("");

        result.Document.Should().BeNull();
        result.Diagnostics.Should().ContainSingle(d => d.Kind == TolerantRecoveryKind.Failed);
    }

    [Fact]
    public void ParseArray_FencedAndProse_ReturnsArrayDocument()
    {
        var raw = """
            ```json
            here is your array:
            [{"x":1},{"x":2}]
            ```
            """;

        var result = _parser.ParseArray(raw);

        result.Document.Should().NotBeNull();
        result.Document!.RootElement.ValueKind.Should().Be(JsonValueKind.Array);
        result.Document.RootElement.GetArrayLength().Should().Be(2);
    }
}
