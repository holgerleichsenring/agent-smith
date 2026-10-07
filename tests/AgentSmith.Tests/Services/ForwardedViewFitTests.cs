using System.Text.RegularExpressions;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Services.Providers.Agent;
using FluentAssertions;
using Microsoft.Extensions.AI;

namespace AgentSmith.Tests.Services;

/// <summary>
/// 2026-10-07-6b9dc: the finaliser's forwarded copy is re-cut largest first until it fits;
/// the stored history is never touched and a cut result keeps its true total behind one marker.
/// </summary>
public sealed class ForwardedViewFitTests
{
    [Fact]
    public void Fit_OneHugeResult_ShrinksItAndKeepsOthers()
    {
        var messages = Loop(("a", Text(400_000)), ("b", Text(8_000)), ("c", Text(6_000)));

        var fitted = ForwardedViewFit.Fit(messages, targetTokens: 25_000);

        CompactingChatClient.EstimateTokens(fitted).Should().BeLessThan(25_000);
        ResultText(fitted, "a").Length.Should().BeLessThan(400_000);
        ResultText(fitted, "b").Should().Be(ResultText(messages, "b"));
        ResultText(fitted, "c").Should().Be(ResultText(messages, "c"));
    }

    [Fact]
    public void Fit_AlreadyUnderTarget_ReturnsSameMessages()
    {
        var messages = Loop(("a", Text(8_000)));

        ForwardedViewFit.Fit(messages, targetTokens: 10_000).Should().BeSameAs(messages);
    }

    [Fact]
    public void Fit_StoredHistory_IsNotMutated()
    {
        var messages = Loop(("a", Text(400_000)), ("b", Text(300_000)));
        var storedMessages = messages.ToList();
        var storedResults = messages.SelectMany(m => m.Contents).OfType<FunctionResultContent>().ToList();

        var fitted = ForwardedViewFit.Fit(messages, targetTokens: 20_000);

        messages.Should().Equal(storedMessages);
        messages.SelectMany(m => m.Contents).OfType<FunctionResultContent>().Should().Equal(storedResults);
        storedResults.Select(r => ((string)r.Result!).Length).Should().Equal(400_000, 300_000);
        fitted.Should().NotBeSameAs(messages);
        fitted.Select(m => m.Role).Should().Equal(messages.Select(m => m.Role));
        fitted.SelectMany(m => m.Contents).OfType<FunctionResultContent>().Select(r => r.CallId)
            .Should().Equal("a", "b");
    }

    [Fact]
    public void Fit_AlreadyBoundedResult_KeepsTrueTotalAndOneMarker()
    {
        var bounded = (string)ToolResultBound.Apply(Text(1_040_000), ToolResultBound.DefaultBudget)!;
        var messages = Loop(("a", bounded));

        var fitted = ForwardedViewFit.Fit(messages, targetTokens: 10_000);

        var text = ResultText(fitted, "a");
        text.Length.Should().BeLessThan(40_000);
        Regex.Matches(text, "characters cut from the").Should().ContainSingle();
        ToolResultBound.StatedTotal(text).Should().Be(1_040_000);
    }

    [Fact]
    public void Fit_UnmarkedResult_ShrinksWithTrueTotal()
    {
        var messages = Loop(("a", Text(200_000)));

        var fitted = ForwardedViewFit.Fit(messages, targetTokens: 10_000);

        var text = ResultText(fitted, "a");
        text.Length.Should().BeLessThan(40_000);
        ToolResultBound.StatedTotal(text).Should().Be(200_000);
    }

    [Fact]
    public void Fit_FiveBoundedResultsAgainstAWindowOf100000_Fits()
    {
        var bounded = Enumerable.Range(0, 5).Select(i => ($"r{i}", Text(ToolResultBound.DefaultBudget))).ToArray();
        var target = (int)(100_000 * 0.85);

        var fitted = ForwardedViewFit.Fit(Loop(bounded), target);

        CompactingChatClient.EstimateTokens(fitted).Should().BeLessThan(target);
    }

    [Fact]
    public void Fit_EveryResultAtTheFloor_StopsOverTarget()
    {
        var messages = Loop(("a", Text(50_000)));
        messages.Insert(0, new ChatMessage(ChatRole.System, Text(80_000)));

        var fitted = ForwardedViewFit.Fit(messages, targetTokens: 10_000);

        ResultText(fitted, "a").Length.Should().BeLessThanOrEqualTo(ForwardedViewFit.FloorChars);
        CompactingChatClient.EstimateTokens(fitted).Should().BeGreaterThanOrEqualTo(10_000);
    }

    internal static List<ChatMessage> Loop(params (string CallId, string Text)[] results)
    {
        var messages = new List<ChatMessage> { new(ChatRole.User, "the ticket") };
        foreach (var (callId, text) in results)
        {
            messages.Add(new ChatMessage(ChatRole.Assistant, [new FunctionCallContent(callId, "read_file")]));
            messages.Add(new ChatMessage(ChatRole.Tool, [new FunctionResultContent(callId, text)]));
        }
        return messages;
    }

    internal static string Text(int length) => "HEAD" + new string('x', length - 8) + "TAIL";

    private static string ResultText(IEnumerable<ChatMessage> messages, string callId) =>
        (string)messages.SelectMany(m => m.Contents).OfType<FunctionResultContent>()
            .Single(r => r.CallId == callId).Result!;
}
