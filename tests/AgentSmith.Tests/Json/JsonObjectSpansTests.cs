using AgentSmith.Contracts.Json;
using FluentAssertions;

namespace AgentSmith.Tests.Json;

/// <summary>
/// 2026-09-07-24ed: the one balanced-span scanner every parser of model output reads
/// through. A brace inside a string literal is text; a quote in prose before the
/// object is not a string boundary; an unclosed span does not hide the span after it.
/// </summary>
public sealed class JsonObjectSpansTests
{
    [Fact]
    public void Balanced_ABraceInsideAString_DoesNotCloseOrOpenAnObject()
    {
        var spans = JsonObjectSpans.Balanced("""{"open": "{", "close": "}", "n": 1}""").ToList();

        spans.Should().ContainSingle().Which.Should().EndWith("\"n\": 1}");
    }

    [Fact]
    public void Balanced_ProseQuotesBeforeTheObject_AreNotStringBoundaries()
    {
        var text = "He said \"go on and {\"a\":1}";

        JsonObjectSpans.Balanced(text).Should().Equal("""{"a":1}""");
    }

    [Fact]
    public void Balanced_AnUnclosedObject_IsSkippedAndTheNextOneFound()
    {
        var text = "a stray { in prose, then {\"a\":1} and a tail {\"b\":";

        JsonObjectSpans.Balanced(text).Should().Equal("""{"a":1}""");
    }

    [Fact]
    public void Balanced_NestedObjects_YieldTheOutermostSpan()
    {
        JsonObjectSpans.Balanced("""{"outer": {"inner": 1}} {"second": 2}""")
            .Should().Equal("""{"outer": {"inner": 1}}""", """{"second": 2}""");
    }

    [Fact]
    public void Balanced_AnEscapedQuoteInsideAString_DoesNotEndTheString()
    {
        var spans = JsonObjectSpans.Balanced("""{"s": "say \"{\" now", "n": 1}""").ToList();

        spans.Should().ContainSingle().Which.Should().EndWith("\"n\": 1}");
    }
}
