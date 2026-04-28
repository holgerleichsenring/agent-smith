using System.Text.RegularExpressions;
using AgentSmith.Contracts.Models;
using AgentSmith.Infrastructure.Services.Security;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Services;

public sealed class ApiAuthPatternTests
{
    private const string ApiAuthCategory = "api-auth";

    private static IReadOnlyList<PatternDefinition> LoadApiAuthPatterns()
    {
        var loader = new PatternDefinitionLoader(NullLogger<PatternDefinitionLoader>.Instance);
        var patternsDir = Path.Combine(
            Directory.GetCurrentDirectory().Split("bin")[0], "..", "..", "config", "patterns");
        var all = loader.LoadFromDirectory(Path.GetFullPath(patternsDir));
        return all.Where(p => p.Category == ApiAuthCategory).ToList();
    }

    private static PatternDefinition? FindMatching(IEnumerable<PatternDefinition> patterns, string sample) =>
        patterns.FirstOrDefault(p => Regex.IsMatch(sample, p.Regex));

    [Fact]
    public void ApiAuthCategory_PatternsLoaded()
    {
        var patterns = LoadApiAuthPatterns();
        patterns.Should().NotBeEmpty("config/patterns/api-auth.yaml must define API-auth patterns");
    }

    [Fact]
    public void ApiAuthPatterns_AllRegexCompile()
    {
        var patterns = LoadApiAuthPatterns();
        foreach (var p in patterns)
        {
            var act = () => new Regex(p.Regex);
            act.Should().NotThrow($"pattern {p.Id} regex must compile");
        }
    }

    [Fact]
    public void ApiAuthPatterns_AllSeveritiesValid()
    {
        var allowed = new[] { "info", "low", "medium", "high", "critical" };
        var patterns = LoadApiAuthPatterns();
        foreach (var p in patterns)
        {
            allowed.Should().Contain(p.Severity, $"pattern {p.Id} severity must be a known value");
        }
    }

    [Fact]
    public void ValidateLifetimeFalseSample_FlaggedAsCritical()
    {
        var match = FindMatching(LoadApiAuthPatterns(), "ValidateLifetime = false");
        match.Should().NotBeNull("disabling JWT lifetime validation should be flagged");
        match!.Severity.Should().Be("critical");
    }

    [Fact]
    public void CorsAllowAnyWithCredentialsSamples_FlaggedAsHigh()
    {
        var patterns = LoadApiAuthPatterns();
        foreach (var sample in new[]
        {
            ".AllowAnyOrigin().AllowCredentials()",
            ".AllowCredentials().AllowAnyOrigin()",
        })
        {
            var match = FindMatching(patterns, sample);
            match.Should().NotBeNull($"CORS sample '{sample}' should be flagged");
            match!.Severity.Should().Be("high");
        }
    }

    [Fact]
    public void AllowAnonymousOnPostSample_FlaggedAsHigh()
    {
        const string sample = "[AllowAnonymous]\n[HttpPost(\"/api/x\")]\npublic IActionResult Create() {}";
        var match = FindMatching(LoadApiAuthPatterns(), sample);
        match.Should().NotBeNull("anonymous on state-changing verb should be flagged");
        match!.Severity.Should().Be("high");
    }

    [Fact]
    public void HardcodedJwtSecretSample_FlaggedAsCritical()
    {
        var match = FindMatching(LoadApiAuthPatterns(), "JwtKey = \"super-secret-key-123456-abcdef\"");
        match.Should().NotBeNull("hardcoded JWT secret should be flagged");
        match!.Severity.Should().Be("critical");
    }

    [Fact]
    public void ValidateIssuerFalseSample_Flagged()
    {
        FindMatching(LoadApiAuthPatterns(), "ValidateIssuer = false")
            .Should().NotBeNull("disabling issuer validation should be flagged");
    }

    [Fact]
    public void ValidateIssuerSigningKeyFalseSample_Flagged()
    {
        FindMatching(LoadApiAuthPatterns(), "ValidateIssuerSigningKey = false")
            .Should().NotBeNull("disabling signing-key validation should be flagged");
    }
}
