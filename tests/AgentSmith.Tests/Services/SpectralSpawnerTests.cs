using AgentSmith.Infrastructure.Services.Spectral;
using FluentAssertions;

namespace AgentSmith.Tests.Services;

public sealed class SpectralSpawnerTests
{
    [Fact]
    public void ParseJsonOutput_ValidFindings_ParsesCorrectly()
    {
        var output = """
            [
              {
                "code": "owasp:api3:2019-define-error-responses-401",
                "message": "Missing 401 response.",
                "path": ["paths", "/api/users", "get", "responses"],
                "severity": 1,
                "range": { "start": { "line": 42 } }
              },
              {
                "code": "owasp:api4:2019-rate-limit",
                "message": "Missing rate limiting headers.",
                "path": ["paths", "/api/login", "post"],
                "severity": 0,
                "range": { "start": { "line": 87 } }
              }
            ]
            """;

        var findings = SpectralSpawner.ParseJsonOutput(output);

        findings.Should().HaveCount(2);
        findings[0].Code.Should().Be("owasp:api3:2019-define-error-responses-401");
        findings[0].Message.Should().Be("Missing 401 response.");
        findings[0].Path.Should().Be("paths./api/users.get.responses");
        findings[0].Severity.Should().Be("warn");
        findings[0].Line.Should().Be(42);
        findings[1].Code.Should().Be("owasp:api4:2019-rate-limit");
        findings[1].Severity.Should().Be("error");
        findings[1].Line.Should().Be(87);
    }

    [Fact]
    public void ParseJsonOutput_EmptyOutput_ReturnsEmpty()
    {
        var findings = SpectralSpawner.ParseJsonOutput("");
        findings.Should().BeEmpty();
    }

    [Fact]
    public void ParseJsonOutput_EmptyArray_ReturnsEmpty()
    {
        var findings = SpectralSpawner.ParseJsonOutput("[]");
        findings.Should().BeEmpty();
    }

    [Fact]
    public void ParseJsonOutput_InvalidJson_ReturnsEmpty()
    {
        var findings = SpectralSpawner.ParseJsonOutput("not valid json");
        findings.Should().BeEmpty();
    }

    [Theory]
    [InlineData(0, "error")]
    [InlineData(1, "warn")]
    [InlineData(2, "info")]
    [InlineData(3, "hint")]
    [InlineData(-1, "unknown")]
    [InlineData(99, "unknown")]
    public void MapSeverity_MapsCorrectly(int level, string expected)
    {
        SpectralSpawner.MapSeverity(level).Should().Be(expected);
    }

    [Fact]
    public void ParseJsonOutput_MissingOptionalFields_DefaultsGracefully()
    {
        var output = """
            [
              {
                "code": "some-rule",
                "message": "Something is wrong"
              }
            ]
            """;

        var findings = SpectralSpawner.ParseJsonOutput(output);

        findings.Should().HaveCount(1);
        findings[0].Code.Should().Be("some-rule");
        findings[0].Message.Should().Be("Something is wrong");
        findings[0].Path.Should().BeEmpty();
        findings[0].Severity.Should().Be("unknown");
        findings[0].Line.Should().Be(0);
    }
}
