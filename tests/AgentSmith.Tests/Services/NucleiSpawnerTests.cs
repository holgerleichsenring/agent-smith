using AgentSmith.Infrastructure.Services.Nuclei;
using FluentAssertions;

namespace AgentSmith.Tests.Services;

public sealed class NucleiSpawnerTests
{
    [Fact]
    public void ParseJsonLines_ValidFindings_ParsesCorrectly()
    {
        var output = """
            {"template-id":"sql-injection","info":{"name":"SQL Injection","severity":"high","description":"Found SQL injection"},"matched-at":"https://api.example.com/users?id=1"}
            {"template-id":"xss-reflected","info":{"name":"Reflected XSS","severity":"medium","reference":["https://owasp.org/xss"]},"matched-at":"https://api.example.com/search?q=test"}
            """;

        var findings = NucleiSpawner.ParseJsonLines(output);

        findings.Should().HaveCount(2);
        findings[0].TemplateId.Should().Be("sql-injection");
        findings[0].Name.Should().Be("SQL Injection");
        findings[0].Severity.Should().Be("high");
        findings[0].MatchedUrl.Should().Be("https://api.example.com/users?id=1");
        findings[0].Description.Should().Be("Found SQL injection");
        findings[1].TemplateId.Should().Be("xss-reflected");
        findings[1].Severity.Should().Be("medium");
        findings[1].Reference.Should().Contain("https://owasp.org/xss");
    }

    [Fact]
    public void ParseJsonLines_EmptyOutput_ReturnsEmpty()
    {
        var findings = NucleiSpawner.ParseJsonLines("");
        findings.Should().BeEmpty();
    }

    [Theory]
    [InlineData("https://localhost:5000", "https://host.docker.internal:5000")]
    [InlineData("https://127.0.0.1:8080", "https://host.docker.internal:8080")]
    [InlineData("https://api.example.com", "https://api.example.com")]
    public void RewriteLocalhostForDocker_ReplacesCorrectly(string input, string expected)
    {
        NucleiSpawner.RewriteLocalhostForDocker(input).Should().Be(expected);
    }

    [Fact]
    public void ParseJsonLines_MixedWithStatusLines_SkipsNonJson()
    {
        var output = """
            [INF] Loading templates...
            {"template-id":"cors-check","info":{"name":"CORS Misconfiguration","severity":"medium"},"matched-at":"https://api.example.com/"}
            [INF] Scan complete
            """;

        var findings = NucleiSpawner.ParseJsonLines(output);

        findings.Should().HaveCount(1);
        findings[0].Name.Should().Be("CORS Misconfiguration");
    }
}
