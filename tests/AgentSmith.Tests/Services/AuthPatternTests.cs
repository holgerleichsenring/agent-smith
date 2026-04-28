using System.Text.RegularExpressions;
using AgentSmith.Contracts.Models;
using AgentSmith.Infrastructure.Services.Security;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Services;

public sealed class AuthPatternTests
{
    private const string AuthCategory = "auth";

    private static IReadOnlyList<PatternDefinition> LoadAuthPatterns()
    {
        var dir = TestPatternsDirectory.Resolve();
        if (dir is null) return [];

        var loader = new PatternDefinitionLoader(NullLogger<PatternDefinitionLoader>.Instance);
        var all = loader.LoadFromDirectory(dir);
        return all.Where(p => p.Category == AuthCategory).ToList();
    }

    private static bool AnyMatches(IEnumerable<PatternDefinition> patterns, string sample) =>
        patterns.Any(p => Regex.IsMatch(sample, p.Regex));

    [Fact]
    public void AuthCategory_PatternsLoaded()
    {
        if (!TestPatternsDirectory.IsAvailable()) return;
        var patterns = LoadAuthPatterns();
        patterns.Should().NotBeEmpty("auth category in agentsmith-skills must contain IDOR patterns");
    }

    [Fact]
    public void AuthPatterns_AllRegexCompile()
    {
        if (!TestPatternsDirectory.IsAvailable()) return;
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
        if (!TestPatternsDirectory.IsAvailable()) return;
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
        if (!TestPatternsDirectory.IsAvailable()) return;
        var patterns = LoadAuthPatterns();
        const string sample = "[HttpGet(\"users/{id:int}\")]";
        AnyMatches(patterns, sample)
            .Should().BeTrue("sequential integer ID in route should be flagged as IDOR candidate");
    }

    [Fact]
    public void EfFindByIdSample_MatchedByAtLeastOneAuthPattern()
    {
        if (!TestPatternsDirectory.IsAvailable()) return;
        var patterns = LoadAuthPatterns();
        const string sample = "var user = dbContext.Users.Find(id);";
        AnyMatches(patterns, sample)
            .Should().BeTrue("EF Find(id) without ownership predicate should be flagged");
    }

    [Fact]
    public void LinqSinglePredicateSample_MatchedByAtLeastOneAuthPattern()
    {
        if (!TestPatternsDirectory.IsAvailable()) return;
        var patterns = LoadAuthPatterns();
        const string sample = ".FirstOrDefault(u => u.Id == userId)";
        AnyMatches(patterns, sample)
            .Should().BeTrue("LINQ single-predicate ID lookup should be flagged");
    }

    [Fact]
    public void OwnershipPredicateSample_NotMatchedByAuthPatterns()
    {
        if (!TestPatternsDirectory.IsAvailable()) return;
        var patterns = LoadAuthPatterns();
        const string safeSample = ".FirstOrDefault(u => u.Id == id && u.TenantId == currentTenant)";
        AnyMatches(patterns, safeSample)
            .Should().BeFalse("multi-predicate query with tenant scoping should not be flagged");
    }
}
