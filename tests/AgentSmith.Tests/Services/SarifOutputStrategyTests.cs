using System.Text.Json;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Services.Output;
using FluentAssertions;

namespace AgentSmith.Tests.Services;

public sealed class SarifOutputStrategyTests
{
    [Fact]
    public void BuildSarifDocument_EmptyFindings_ProducesValidSarif()
    {
        var sarif = SarifOutputStrategy.BuildSarifDocument([]);
        var json = sarif.ToJsonString();

        json.Should().Contain("\"version\":\"2.1.0\"");
        json.Should().Contain("Agent Smith Security");
        json.Should().Contain("\"results\":[]");
    }

    [Fact]
    public void BuildSarifDocument_WithFindings_MapsCorrectly()
    {
        var findings = new List<Finding>
        {
            new("HIGH", "src/Api/UserController.cs", 47, 52, "SQL injection", "Unsanitized input", 9),
            new("MEDIUM", "src/Auth/TokenService.cs", 23, null, "JWT secret", "No validation", 8),
        };

        var sarif = SarifOutputStrategy.BuildSarifDocument(findings);
        var json = sarif.ToJsonString();
        var doc = JsonDocument.Parse(json);

        var runs = doc.RootElement.GetProperty("runs");
        var results = runs[0].GetProperty("results");
        results.GetArrayLength().Should().Be(2);

        var first = results[0];
        first.GetProperty("ruleId").GetString().Should().Be("AS001");
        first.GetProperty("level").GetString().Should().Be("error");

        var second = results[1];
        second.GetProperty("ruleId").GetString().Should().Be("AS002");
        second.GetProperty("level").GetString().Should().Be("warning");
    }

    [Fact]
    public void BuildSarifDocument_DuplicateTitles_ShareRuleId()
    {
        var findings = new List<Finding>
        {
            new("HIGH", "src/A.cs", 10, null, "SQL injection", "First instance", 9),
            new("HIGH", "src/B.cs", 20, null, "SQL injection", "Second instance", 9),
        };

        var sarif = SarifOutputStrategy.BuildSarifDocument(findings);
        var json = sarif.ToJsonString();
        var doc = JsonDocument.Parse(json);

        var rules = doc.RootElement.GetProperty("runs")[0]
            .GetProperty("tool").GetProperty("driver").GetProperty("rules");
        rules.GetArrayLength().Should().Be(1, "duplicate titles should share a rule");

        var results = doc.RootElement.GetProperty("runs")[0].GetProperty("results");
        results[0].GetProperty("ruleId").GetString().Should().Be("AS001");
        results[1].GetProperty("ruleId").GetString().Should().Be("AS001");
    }

    [Theory]
    [InlineData("HIGH", "error")]
    [InlineData("MEDIUM", "warning")]
    [InlineData("LOW", "note")]
    [InlineData("unknown", "note")]
    public void MapSeverity_CorrectMapping(string input, string expected)
    {
        SarifOutputStrategy.MapSeverity(input).Should().Be(expected);
    }

    [Fact]
    public void CompressToBase64Gzip_ProducesNonEmptyString()
    {
        var json = "{\"test\": true}";
        var compressed = SarifOutputStrategy.CompressToBase64Gzip(json);

        compressed.Should().NotBeNullOrEmpty();
        Convert.FromBase64String(compressed).Should().NotBeEmpty();
    }
}
