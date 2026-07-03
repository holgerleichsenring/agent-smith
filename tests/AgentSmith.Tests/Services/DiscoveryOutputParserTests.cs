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
    [Fact]
    public void TryParse_CleanJson_Parses()
    {
        var ok = DiscoveryOutputParser.TryParse(
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

        var ok = DiscoveryOutputParser.TryParse(raw, out var output, out var error);

        ok.Should().BeTrue(error);
        output!.Status.Should().Be("ok");
    }

    [Fact]
    public void TryParse_NoJsonAtAll_ReturnsFailure()
    {
        var ok = DiscoveryOutputParser.TryParse(
            "I could not analyze this repository.", out var output, out var error);

        ok.Should().BeFalse();
        output.Should().BeNull();
        error.Should().NotBeEmpty();
    }
}
