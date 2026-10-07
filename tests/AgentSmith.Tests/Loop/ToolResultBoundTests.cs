using System.Text.Json;
using AgentSmith.Contracts.Services;
using FluentAssertions;

namespace AgentSmith.Tests.Loop;

/// <summary>
/// 2026-10-07-6b9da: one bound for every tool result — head and tail kept, the whole output
/// within budget, the true total named, the unshown part not claimed absent (485e), and a way
/// to page for the rest.
/// </summary>
public sealed class ToolResultBoundTests
{
    private const int Budget = 2_000;

    [Fact]
    public void Apply_UnderBudget_ReturnsSameText()
    {
        var text = new string('a', 500);

        ToolResultBound.Apply(text, Budget).Should().Be(text);
    }

    [Fact]
    public void Apply_WithinBudget_IsUnchanged()
    {
        var once = (string)ToolResultBound.Apply(Long(50_000), Budget)!;

        ToolResultBound.Apply(once, Budget).Should().Be(once, "a second pass must keep the first cut's total");
        ToolResultBound.Apply(new string('b', Budget), Budget).Should().Be(new string('b', Budget));
    }

    [Fact]
    public void Apply_OverBudget_OutputWithinBudgetKeepsHeadTailNamesTotal()
    {
        var bound = (string)ToolResultBound.Apply(Long(50_000), Budget)!;

        bound.Length.Should().BeLessThanOrEqualTo(Budget);
        bound.Should().StartWith("HEAD").And.EndWith("TAIL");
        bound.Should().Contain("of 50,000 characters cut from the middle")
            .And.Contain("may still exist").And.Contain("not a complete listing")
            .And.Contain("read_file start_line/line_count").And.Contain("narrower path or pattern");
        Dropped(bound).Should().Be(50_000 - (bound.Length - MarkerLength(bound)));
    }

    [Fact]
    public void Apply_KnownTotal_MarkerStatesIt()
    {
        var bound = (string)ToolResultBound.Apply(Long(50_000), Budget, knownTotal: 1_500_000)!;

        bound.Should().Contain("of 1,500,000 characters");
        bound.Length.Should().BeLessThanOrEqualTo(Budget);
    }

    [Fact]
    public void Apply_JsonStringResult_ReturnsPlainString()
    {
        var json = JsonSerializer.SerializeToElement("line \"one\"\n\tline two");

        ToolResultBound.Apply(json, Budget).Should().Be("line \"one\"\n\tline two");
        ((string)ToolResultBound.Apply(JsonSerializer.SerializeToElement(Long(50_000)), Budget)!)
            .Length.Should().BeLessThanOrEqualTo(Budget);
    }

    [Fact]
    public void Apply_NonStringResult_PassesThroughUnchanged()
    {
        var json = JsonSerializer.SerializeToElement(new { files = 3 });

        ToolResultBound.Apply(json, Budget).Should().Be(json);
        ToolResultBound.Apply(null, Budget).Should().BeNull();
    }

    [Fact]
    public void Recut_BoundedText_KeepsOriginalTotalAndOneMarker()
    {
        var once = (string)ToolResultBound.Apply(Long(50_000), 10_000)!;

        var recut = ToolResultBound.Recut(once, Budget);

        recut.Length.Should().BeLessThanOrEqualTo(Budget);
        recut.Should().StartWith("HEAD").And.EndWith("TAIL").And.Contain("of 50,000 characters");
        recut.Split("characters cut from the").Should().HaveCount(2, "one marker, not a marker inside a marker");
        Dropped(recut).Should().Be(50_000 - (recut.Length - MarkerLength(recut)));
    }

    [Fact]
    public void Recut_UnmarkedText_BehavesAsApply()
    {
        var text = Long(50_000);

        ToolResultBound.Recut(text, Budget).Should().Be((string)ToolResultBound.Apply(text, Budget)!);
    }

    [Fact]
    public void Recut_HeadOnlyCutText_KeepsItsStatedTotal()
    {
        var headOnly = "HEAD" + new string('h', 39_996)
            + "\n… [truncated: 960000 of 1000000 characters omitted — read a narrower range, or grep for what you need]";

        var recut = ToolResultBound.Recut(headOnly, Budget);

        recut.Length.Should().BeLessThanOrEqualTo(Budget);
        recut.Should().StartWith("HEAD").And.Contain("of 1,000,000 characters cut from the end")
            .And.NotContain("[truncated:");
    }

    [Fact]
    public void TextOf_StringOrJsonString_ReturnsTextOtherwiseNull()
    {
        ToolResultBound.TextOf("plain").Should().Be("plain");
        ToolResultBound.TextOf(JsonSerializer.SerializeToElement("json")).Should().Be("json");
        ToolResultBound.TextOf(JsonSerializer.SerializeToElement(42)).Should().BeNull();
    }

    // 2026-10-07-6b9db: a head and a tail held apart — the cut falls between them, never inside
    // the tail, and the whole output names the total of what the pieces came from.
    [Fact]
    public void ApplyParts_HeadAndTailWithAGap_CutBetweenThemAndNamesTheTotal()
    {
        var bound = ToolResultBound.ApplyParts("HEAD" + new string('h', 5_000), "TAIL-END", Budget, 900_000);

        bound.Length.Should().BeLessThanOrEqualTo(Budget);
        bound.Should().StartWith("HEAD").And.EndWith("TAIL-END");
        bound.Should().Contain("of 900,000 characters cut from the middle");
    }

    [Fact]
    public void ApplyParts_WholeOutputWithinBudget_ReturnsTheJoinedText()
    {
        ToolResultBound.ApplyParts("head ", "tail", Budget, 9).Should().Be("head tail");
    }

    [Fact]
    public void ApplyParts_NoTailAndATotalPastTheHead_SaysOnlyTheHeadIsShown()
    {
        var bound = ToolResultBound.ApplyParts("HEAD", string.Empty, Budget, 1_000_000);

        bound.Should().StartWith("HEAD").And.Contain("of 1,000,000 characters cut from the end");
    }

    private static string Long(int length) => "HEAD" + new string('m', length - 8) + "TAIL";

    private static long Dropped(string bound)
    {
        var start = bound.IndexOf("[… ", StringComparison.Ordinal) + 3;
        var number = bound[start..bound.IndexOf(" of ", start, StringComparison.Ordinal)];
        return long.Parse(number.Replace(",", ""));
    }

    private static int MarkerLength(string bound)
    {
        var start = bound.IndexOf("\n\n[… ", StringComparison.Ordinal);
        return bound.IndexOf("]\n\n", start, StringComparison.Ordinal) + 3 - start;
    }
}
