using System.Text.RegularExpressions;
using AgentSmith.Contracts.Models;
using AgentSmith.Infrastructure.Services.Security;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Services;

public sealed class AuthPatternTests
{
    private const string AuthCategory = "auth";

    private static IReadOnlyList<PatternDefinition> LoadAuthPatterns()
    {
        var loader = new PatternDefinitionLoader(NullLogger<PatternDefinitionLoader>.Instance);
        var patternsDir = Path.Combine(
            Directory.GetCurrentDirectory().Split("bin")[0],
            "..", "..", "config", "patterns");
        var all = loader.LoadFromDirectory(Path.GetFullPath(patternsDir));
        return all.Where(p => p.Category == AuthCategory).ToList();
    }

    private static bool AnyMatches(IEnumerable<PatternDefinition> patterns, string sample) =>
        patterns.Any(p => Regex.IsMatch(sample, p.Regex));

    [Fact]
    public void AuthCategory_PatternsLoaded()
    {
        var patterns = LoadAuthPatterns();
        patterns.Should().NotBeEmpty("config/patterns/auth.yaml must exist and contain IDOR patterns");
    }

    [Fact]
    public void AuthPatterns_AllRegexCompile()
    {
        var patterns = LoadAuthPatterns();
        foreach (var p in patterns)
        {
            var act = () => new Regex(p.Regex);
            act.Should().NotThrow($"pattern {p.Id} regex must compile");
        }
    }

    [Fact]
    public void AuthPatterns_AllSeveritiesValid()
    {
        var allowed = new[] { "info", "low", "medium", "high", "critical" };
        var patterns = LoadAuthPatterns();
        foreach (var p in patterns)
        {
            allowed.Should().Contain(p.Severity, $"pattern {p.Id} severity must be a known value");
        }
    }

    [Fact]
    public void AspnetIntRouteIdorSample_MatchedByAtLeastOneAuthPattern()
    {
        var patterns = LoadAuthPatterns();
        const string sample = "[HttpGet(\"users/{id:int}\")]";
        AnyMatches(patterns, sample)
            .Should().BeTrue("sequential integer ID in route should be flagged as IDOR candidate");
    }

    [Fact]
    public void EfFindByIdSample_MatchedByAtLeastOneAuthPattern()
    {
        var patterns = LoadAuthPatterns();
        const string sample = "var user = dbContext.Users.Find(id);";
        AnyMatches(patterns, sample)
            .Should().BeTrue("EF Find(id) without ownership predicate should be flagged");
    }

    [Fact]
    public void LinqSinglePredicateSample_MatchedByAtLeastOneAuthPattern()
    {
        var patterns = LoadAuthPatterns();
        const string sample = ".FirstOrDefault(u => u.Id == userId)";
        AnyMatches(patterns, sample)
            .Should().BeTrue("LINQ single-predicate ID lookup should be flagged");
    }

    [Fact]
    public void OwnershipPredicateSample_NotMatchedByAuthPatterns()
    {
        var patterns = LoadAuthPatterns();
        const string safeSample = ".FirstOrDefault(u => u.Id == id && u.TenantId == currentTenant)";
        AnyMatches(patterns, safeSample)
            .Should().BeFalse("multi-predicate query with tenant scoping should not be flagged");
    }
}
