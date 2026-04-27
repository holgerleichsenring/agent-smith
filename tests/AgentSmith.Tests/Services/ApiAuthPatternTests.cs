using System.Text.RegularExpressions;
using AgentSmith.Contracts.Models;
using AgentSmith.Infrastructure.Services.Security;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Services;

public sealed class ApiAuthPatternTests
{
    private static IReadOnlyList<PatternDefinition> LoadApiAuthPatterns()
    {
        var loader = new PatternDefinitionLoader(NullLogger<PatternDefinitionLoader>.Instance);
        var patternsDir = Path.Combine(
            Directory.GetCurrentDirectory().Split("bin")[0], "..", "..", "config", "patterns");
        var all = loader.LoadFromDirectory(Path.GetFullPath(patternsDir));
        return all.Where(p => p.Category == "api-auth").ToList();
    }

    private static PatternDefinition Get(string id) =>
        LoadApiAuthPatterns().Single(p => p.Id == id);

    [Fact]
    public void ValidateLifetimeFalse_Critical()
    {
        var pat = Get("jwt-validate-lifetime-disabled");
        Regex.IsMatch("ValidateLifetime = false", pat.Regex).Should().BeTrue();
        pat.Severity.Should().Be("critical");
    }

    [Fact]
    public void AllowAnyOriginPlusCredentials_High()
    {
        var pat = Get("cors-allow-any-origin-with-credentials");
        Regex.IsMatch(".AllowAnyOrigin().AllowCredentials()", pat.Regex).Should().BeTrue();
        Regex.IsMatch(".AllowCredentials().AllowAnyOrigin()", pat.Regex).Should().BeTrue();
        pat.Severity.Should().Be("high");
    }

    [Fact]
    public void AllowAnonymousOnPost_High()
    {
        var pat = Get("anonymous-on-state-changing-verb");
        var snippet = "[AllowAnonymous]\n[HttpPost(\"/api/x\")]\npublic IActionResult Create() {}";
        Regex.IsMatch(snippet, pat.Regex).Should().BeTrue();
        pat.Severity.Should().Be("high");
    }

    [Fact]
    public void HardcodedJwtSecret_Critical()
    {
        var pat = Get("hardcoded-jwt-secret");
        Regex.IsMatch("JwtKey = \"super-secret-key-123456-abcdef\"", pat.Regex).Should().BeTrue();
        pat.Severity.Should().Be("critical");
    }

    [Fact]
    public void ValidateIssuerFalse_High()
    {
        var pat = Get("jwt-validate-issuer-disabled");
        Regex.IsMatch("ValidateIssuer = false", pat.Regex).Should().BeTrue();
    }

    [Fact]
    public void ValidateIssuerSigningKeyFalse_Critical()
    {
        var pat = Get("jwt-validate-issuer-signing-key-disabled");
        Regex.IsMatch("ValidateIssuerSigningKey = false", pat.Regex).Should().BeTrue();
    }
}
