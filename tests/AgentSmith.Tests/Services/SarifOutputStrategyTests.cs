using System.Text.Json;
using AgentSmith.Contracts.Models;
using AgentSmith.Infrastructure.Services.Output;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;

namespace AgentSmith.Tests.Services;

public sealed class SarifOutputStrategyTests
{
    [Fact]
    public void BuildSarifDocument_EmptyObservations_ProducesValidSarif()
    {
        var sarif = SarifOutputStrategy.BuildSarifDocument([]);
        var json = sarif.ToJsonString();

        json.Should().Contain("\"version\":\"2.1.0\"");
        json.Should().Contain("Agent Smith Security");
        json.Should().Contain("\"results\":[]");
    }

    [Fact]
    public void BuildSarifDocument_WithObservations_MapsCorrectly()
    {
        var observations = new List<SkillObservation>
        {
            ObservationFactory.Make("HIGH", "src/Api/UserController.cs", 47, "SQL injection", "Unsanitized input", 90, category: "injection"),
            ObservationFactory.Make("MEDIUM", "src/Auth/TokenService.cs", 23, "JWT secret", "No validation", 80, category: "auth"),
        };

        var sarif = SarifOutputStrategy.BuildSarifDocument(observations);
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
    public void BuildSarifDocument_DuplicateCategories_ShareRuleId()
    {
        var observations = new List<SkillObservation>
        {
            ObservationFactory.Make("HIGH", "src/A.cs", 10, "SQL injection", "First instance", 90, category: "injection"),
            ObservationFactory.Make("HIGH", "src/B.cs", 20, "SQL injection", "Second instance", 90, category: "injection"),
        };

        var sarif = SarifOutputStrategy.BuildSarifDocument(observations);
        var json = sarif.ToJsonString();
        var doc = JsonDocument.Parse(json);

        var rules = doc.RootElement.GetProperty("runs")[0]
            .GetProperty("tool").GetProperty("driver").GetProperty("rules");
        rules.GetArrayLength().Should().Be(1, "duplicate categories should share a rule");

        var results = doc.RootElement.GetProperty("runs")[0].GetProperty("results");
        results[0].GetProperty("ruleId").GetString().Should().Be("AS001");
        results[1].GetProperty("ruleId").GetString().Should().Be("AS001");
    }

    [Theory]
    [InlineData(ObservationSeverity.High, "error")]
    [InlineData(ObservationSeverity.Medium, "warning")]
    [InlineData(ObservationSeverity.Low, "note")]
    [InlineData(ObservationSeverity.Info, "none")]
    public void MapSeverity_CorrectMapping(ObservationSeverity input, string expected)
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
