using AgentSmith.Application.Services.Handlers;
using FluentAssertions;

namespace AgentSmith.Tests.Services;

/// <summary>
/// p0294: DiscoveryOutputParser must tolerate a prose-wrapped JSON object (the
/// Sonnet 4.6 preamble that broke init-project's Discover step), not only a clean
/// or fenced document.
/// </summary>
public sealed class DiscoveryOutputParserTests
{
    private static DiscoveryOutputParser Parser => new();

    [Fact]
    public void TryParse_CleanJson_Parses()
    {
        var ok = Parser.TryParse(
            """{"status": "ok", "components": []}""", out var output, out var error);

        ok.Should().BeTrue(error);
        output!.Status.Should().Be("ok");
    }

    [Fact]
    public void TryParse_ProseWrappedJson_Parses()
    {
        const string raw = "Sure! Here is the discovery result:\n"
            + "{\"status\": \"ok\", \"components\": []}\n"
            + "Let me know if you need anything else.";

        var ok = Parser.TryParse(raw, out var output, out var error);

        ok.Should().BeTrue(error);
        output!.Status.Should().Be("ok");
    }

    [Fact]
    public void TryParse_NoJsonAtAll_ReturnsFailure()
    {
        var ok = Parser.TryParse(
            "I could not analyze this repository.", out var output, out var error);

        ok.Should().BeFalse();
        output.Should().BeNull();
        error.Should().NotBeEmpty();
    }
}
