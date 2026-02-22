using AgentSmith.Dispatcher.Services;
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
}
