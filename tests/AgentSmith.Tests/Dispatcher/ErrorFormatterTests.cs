using AgentSmith.Application.Services;
using FluentAssertions;

namespace AgentSmith.Tests.Dispatcher;

public sealed class ErrorFormatterTests
{
    [Theory]
    [InlineData("TF401179: some details", "A pull request for this branch already exists")]
    [InlineData("non-fastforwardable merge happened", "The remote branch has conflicting history")]
    [InlineData("Connection refused to service", "Could not reach a required service")]
    [InlineData("401 Unauthorized access", "Authentication failed")]
    [InlineData("403 Forbidden access denied", "Permission denied")]
    [InlineData("Rate limit exceeded 429", "AI provider rate limit hit")]
    [InlineData("529 overloaded", "AI provider rate limit hit")]
    [InlineData("404 resource not found", "A required resource was not found")]
    [InlineData("operation timeout exceeded", "The operation timed out")]
    [InlineData("No test framework found in repo", "No test framework found")]
    public void Humanize_KnownPatterns_ReturnsFriendlyMessage(string rawError, string expected)
    {
        var result = ErrorFormatter.Humanize(rawError);

        result.Should().Contain(expected);
    }

    [Fact]
    public void Humanize_UnknownError_TruncatesFirstLine()
    {
        var longError = new string('x', 200);

        var result = ErrorFormatter.Humanize(longError);

        result.Length.Should().BeLessThanOrEqualTo(125);
    }

    [Fact]
    public void Humanize_MultilineError_UsesFirstLine()
    {
        var error = "First line error\nSecond line details\nThird line";

        var result = ErrorFormatter.Humanize(error);

        result.Should().NotContain("Second line");
    }

    // p0387: HumanizeRunSummary — only raw provider payloads are translated.

    private const string UsageLimitBody =
        """{"type":"error","error":{"type":"invalid_request_error","message":"You have reached your specified API usage limits. You will regain access on 2026-08-01 at 00:00 UTC."},"request_id":"req_011AbCdEfGh"}""";

    [Fact]
    public void HumanizeRunSummary_AnthropicErrorBody_ExtractsFriendlyMessage()
    {
        var result = ErrorFormatter.HumanizeRunSummary(UsageLimitBody);

        result.Should().NotContain("{").And.NotContain("request_id");
        result.Should().Contain("usage limit");
    }

    [Fact]
    public void HumanizeRunSummary_UsageLimit_NamesResetDate()
    {
        var result = ErrorFormatter.HumanizeRunSummary(UsageLimitBody);

        result.Should().Be("AI provider usage limit reached — access resumes 2026-08-01 00:00 UTC.");
    }

    [Fact]
    public void HumanizeRunSummary_UsageLimitWithoutResetDate_StillFriendly()
    {
        var body = """{"type":"error","error":{"type":"invalid_request_error","message":"You have reached your specified API usage limits."}}""";

        var result = ErrorFormatter.HumanizeRunSummary(body);

        result.Should().StartWith("AI provider usage limit reached");
        result.Should().NotContain("{");
    }

    [Fact]
    public void HumanizeRunSummary_ErrorBodyMatchingExistingRule_AppliesRule()
    {
        var body = """{"type":"error","error":{"type":"overloaded_error","message":"rate limit exceeded, please retry"}}""";

        var result = ErrorFormatter.HumanizeRunSummary(body);

        result.Should().Be("AI provider rate limit hit. Wait a moment and retry.");
    }

    [Fact]
    public void HumanizeRunSummary_ErrorBodyWithoutMatchingRule_ReturnsInnerMessageNeverRawBlob()
    {
        var body = """{"type":"error","error":{"type":"api_error","message":"Something unusual happened inside the provider."}}""";

        var result = ErrorFormatter.HumanizeRunSummary(body);

        result.Should().Be("Something unusual happened inside the provider.");
    }

    [Fact]
    public void HumanizeRunSummary_ErrorBodyEmbeddedInPrefixedText_ExtractsMessage()
    {
        var summary = $"Provider call failed: {UsageLimitBody}";

        var result = ErrorFormatter.HumanizeRunSummary(summary);

        result.Should().Be("AI provider usage limit reached — access resumes 2026-08-01 00:00 UTC.");
    }

    [Fact]
    public void HumanizeRunSummary_CuratedProseSummary_Unchanged()
    {
        // Deliberate prose with rule-trigger words ("timed out", "not found") and
        // over 120 chars — it must survive byte-for-byte, never truncated or matched.
        var curated =
            "Keystone verdict: the contract was not satisfied — the sample repository's integration test timed out and the expected " +
            "changelog entry was not found; the run stopped before committing so no partial work was pushed.";

        var result = ErrorFormatter.HumanizeRunSummary(curated);

        result.Should().Be(curated);
    }

    [Fact]
    public void HumanizeRunSummary_NonErrorJson_Unchanged()
    {
        var json = """{"type":"result","summary":"3 files changed","error":null}""";

        var result = ErrorFormatter.HumanizeRunSummary(json);

        result.Should().Be(json);
    }

    [Fact]
    public void HumanizeRunSummary_EmptySummary_Unchanged()
    {
        ErrorFormatter.HumanizeRunSummary(string.Empty).Should().BeEmpty();
    }
}
