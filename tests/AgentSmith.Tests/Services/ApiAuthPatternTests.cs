using System.Text.RegularExpressions;
using AgentSmith.Contracts.Models;
using AgentSmith.Infrastructure.Services.Security;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Services;

public sealed class ApiAuthPatternTests
{
    private const string ApiAuthCategory = "api-auth";

    private static IReadOnlyList<PatternDefinition> LoadApiAuthPatterns()
    {
        var dir = TestPatternsDirectory.Resolve();
        if (dir is null) return [];

        var loader = new PatternDefinitionLoader(NullLogger<PatternDefinitionLoader>.Instance);
        var all = loader.LoadFromDirectory(dir);
        return all.Where(p => p.Category == ApiAuthCategory).ToList();
    }

    private static PatternDefinition? FindMatching(IEnumerable<PatternDefinition> patterns, string sample) =>
        patterns.FirstOrDefault(p => Regex.IsMatch(sample, p.Regex));

    [Fact]
    public void ApiAuthCategory_PatternsLoaded()
    {
        if (!TestPatternsDirectory.IsAvailable()) return;
        var patterns = LoadApiAuthPatterns();
        patterns.Should().NotBeEmpty("api-auth category in agentsmith-skills must define API-auth patterns");
    }

    [Fact]
    public void ApiAuthPatterns_AllRegexCompile()
    {
        if (!TestPatternsDirectory.IsAvailable()) return;
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
        if (!TestPatternsDirectory.IsAvailable()) return;
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
        if (!TestPatternsDirectory.IsAvailable()) return;
        var match = FindMatching(LoadApiAuthPatterns(), "ValidateLifetime = false");
        match.Should().NotBeNull("disabling JWT lifetime validation should be flagged");
        match!.Severity.Should().Be("critical");
    }

    [Fact]
    public void CorsAllowAnyWithCredentialsSamples_FlaggedAsHigh()
    {
        if (!TestPatternsDirectory.IsAvailable()) return;
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
        if (!TestPatternsDirectory.IsAvailable()) return;
        const string sample = "[AllowAnonymous]\n[HttpPost(\"/api/x\")]\npublic IActionResult Create() {}";
        var match = FindMatching(LoadApiAuthPatterns(), sample);
        match.Should().NotBeNull("anonymous on state-changing verb should be flagged");
        match!.Severity.Should().Be("high");
    }

    [Fact]
    public void HardcodedJwtSecretSample_FlaggedAsCritical()
    {
        if (!TestPatternsDirectory.IsAvailable()) return;
        var match = FindMatching(LoadApiAuthPatterns(), "JwtKey = \"super-secret-key-123456-abcdef\"");
        match.Should().NotBeNull("hardcoded JWT secret should be flagged");
        match!.Severity.Should().Be("critical");
    }

    [Fact]
    public void ValidateIssuerFalseSample_Flagged()
    {
        if (!TestPatternsDirectory.IsAvailable()) return;
        FindMatching(LoadApiAuthPatterns(), "ValidateIssuer = false")
            .Should().NotBeNull("disabling issuer validation should be flagged");
    }

    [Fact]
    public void ValidateIssuerSigningKeyFalseSample_Flagged()
    {
        if (!TestPatternsDirectory.IsAvailable()) return;
        FindMatching(LoadApiAuthPatterns(), "ValidateIssuerSigningKey = false")
            .Should().NotBeNull("disabling signing-key validation should be flagged");
    }
}
