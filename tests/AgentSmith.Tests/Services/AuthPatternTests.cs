using AgentSmith.Contracts.Models;
using AgentSmith.Infrastructure.Services.Security;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Services;

public sealed class AuthPatternTests
{
    private static IReadOnlyList<PatternDefinition> LoadAuthPatterns()
    {
        var loader = new PatternDefinitionLoader(NullLogger<PatternDefinitionLoader>.Instance);
        var patternsDir = Path.Combine(
            Directory.GetCurrentDirectory().Split("bin")[0],
            "..", "..", "config", "patterns");
        var all = loader.LoadFromDirectory(Path.GetFullPath(patternsDir));
        return all.Where(p => p.Category == "auth").ToList();
    }

    [Fact]
    public void AuthCategory_PatternsLoaded()
    {
        var patterns = LoadAuthPatterns();
        patterns.Should().NotBeEmpty("config/patterns/auth.yaml must exist and contain IDOR patterns");
    }

    [Fact]
    public void IntRouteConstraint_Matches_Id()
    {
        var patterns = LoadAuthPatterns();
        var routePattern = patterns.FirstOrDefault(p => p.Id == "aspnet-int-route-constraint");
        routePattern.Should().NotBeNull();

        var sample = "[HttpGet(\"users/{id:int}\")]";
        System.Text.RegularExpressions.Regex.IsMatch(sample, routePattern!.Regex)
            .Should().BeTrue("sequential integer ID in route should be flagged as IDOR candidate");
    }

    [Fact]
    public void EfFindById_Matches()
    {
        var patterns = LoadAuthPatterns();
        var findPattern = patterns.FirstOrDefault(p => p.Id == "ef-find-by-id");
        findPattern.Should().NotBeNull();

        var sample = "var user = dbContext.Users.Find(id);";
        System.Text.RegularExpressions.Regex.IsMatch(sample, findPattern!.Regex)
            .Should().BeTrue("EF Find(id) without ownership predicate should be flagged");
    }

    [Fact]
    public void LinqLoadById_Matches_SinglePredicate()
    {
        var patterns = LoadAuthPatterns();
        var linqPattern = patterns.FirstOrDefault(p => p.Id == "ef-first-by-id-only");
        linqPattern.Should().NotBeNull();

        var sample = ".FirstOrDefault(u => u.Id == userId)";
        System.Text.RegularExpressions.Regex.IsMatch(sample, linqPattern!.Regex)
            .Should().BeTrue("LINQ single-predicate ID lookup should be flagged");
    }

    [Fact]
    public void NormalControllerCode_NoFalsePositive()
    {
        var patterns = LoadAuthPatterns();

        // A properly authorized query with ownership predicate — not a match
        var safeSample = ".FirstOrDefault(u => u.Id == id && u.TenantId == currentTenant)";
        foreach (var p in patterns)
        {
            if (p.Id == "ef-first-by-id-only")
            {
                System.Text.RegularExpressions.Regex.IsMatch(safeSample, p.Regex)
                    .Should().BeFalse("multi-predicate query should not be flagged");
            }
        }
    }
}
