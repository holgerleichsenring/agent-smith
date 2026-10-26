using System.Text.Json;
using AgentSmith.Application.Services.Json;
using AgentSmith.Contracts.Json;
using FluentAssertions;

namespace AgentSmith.Tests.Json;

/// <summary>2026-10-26-cb25d: the shared answer reader and the span scan it stands on.</summary>
public sealed class JsonAnswerReaderTests
{
    private sealed record Row(string? SpecId);

    [Fact]
    public void ReadArray_CitedIdBeforeTheArray_ReadsTheArray() =>
        JsonAnswerReader.ReadArray<Row, string?>("""Checked [c2] first. [{"spec_id":"s1"}]""", r => r.SpecId)
            .Should().Equal("s1");

    [Fact]
    public void ReadArray_EmptyArray_IsAnEmptyAnswerNotNull() =>
        JsonAnswerReader.ReadArray<Row, string?>("Nothing to report: []", r => r.SpecId).Should().BeEmpty();

    [Fact]
    public void ReadArray_NoArray_IsNull() =>
        JsonAnswerReader.ReadArray<Row, string?>("[1, 2] are numbers", r => r.SpecId).Should().BeNull();

    [Fact]
    public void ReadObject_Wire_PassesOverAnObjectTheMapRefuses() =>
        JsonAnswerReader.ReadObject<Row, string>("""{"spec_id":null} then {"spec_id":"s2"}""", r => r.SpecId)
            .Should().Be("s2");

    [Fact]
    public void ReadObject_Element_ReadsAnInnerObjectWhenTheOuterDoesNotMap() =>
        JsonAnswerReader.ReadObject("""{"wrapper":{"verdict":"BUILD"}}""",
                root => root.TryGetProperty("verdict", out var v) ? v.GetString() : null)
            .Should().Be("BUILD");

    [Fact]
    public void ReadObject_BraceInsideAString_DoesNotEndTheObject() =>
        JsonAnswerReader.ReadObject("""Answer: {"quote":"if (x) { y }","verdict":"BUILD"}""",
                root => root.GetProperty("verdict").GetString())
            .Should().Be("BUILD");

    [Fact]
    public void Options_AreSnakeCaseAndCaseInsensitive() =>
        JsonSerializer.Deserialize<Row>("""{"SPEC_ID":"s"}""", JsonAnswerReader.Options)!.SpecId.Should().Be("s");

    [Fact]
    public void Candidates_EveryStartInOrder_InnerIncluded_UnclosedSkipped() =>
        JsonObjectSpans.Candidates("""a {"x":{"y":1}} b {"z" """, '{', '}')
            .Should().Equal("""{"x":{"y":1}}""", """{"y":1}""");
}
