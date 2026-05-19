using System.Text.Json;
using AgentSmith.Contracts.Models;
using AgentSmith.Infrastructure.Services.Output;
using FluentAssertions;

namespace AgentSmith.Tests.Services;

/// <summary>
/// p0147b: output strategies render runtime execution-limit observations in
/// their own section, separate from operator-facing findings, so silent skill
/// drops surface in the final pipeline summary without inflating the
/// Critical/High/Medium/Low/Info tally.
/// </summary>
public sealed class ExecutionLimitOutputTests
{
    private static SkillObservation MakeLimitObservation(
        string category, string description = "skill 'X' hit limit") =>
        new(
            Id: 0,
            Role: "runtime",
            Concern: ObservationConcern.Risk,
            Description: description,
            Suggestion: "raise the budget",
            Blocking: false,
            Severity: ObservationSeverity.Info,
            Confidence: 100,
            EvidenceMode: EvidenceMode.Confirmed,
            Category: category);

    private static SkillObservation MakeFinding() =>
        new(
            Id: 0,
            Role: "investigator",
            Concern: ObservationConcern.Security,
            Description: "SQL injection on /users",
            Suggestion: "Parameterize",
            Blocking: true,
            Severity: ObservationSeverity.High,
            Confidence: 90,
            File: "src/UserController.cs",
            StartLine: 47,
            EvidenceMode: EvidenceMode.AnalyzedFromSource,
            Category: "injection");

    [Fact]
    public void Markdown_RendersExecutionLimitInSeparateSection()
    {
        var observations = new List<SkillObservation>
        {
            MakeFinding(),
            MakeLimitObservation(ExecutionLimitCategories.ExecutionLimitToolCalls, "skill 'X' hit tool-call limit"),
        };

        var md = MarkdownOutputStrategy.BuildMarkdown(observations);

        // The findings tally excludes the limit observation.
        md.Should().Contain("Found **1** issues");
        // The limit observation lives in its own section with a distinct heading.
        md.Should().Contain("## Execution limits hit: 1");
        md.Should().Contain("Tool-call limit");
        md.Should().Contain("hit tool-call limit");
    }

    [Fact]
    public void Markdown_NoLimitObservations_SkipsLimitSection()
    {
        var md = MarkdownOutputStrategy.BuildMarkdown([MakeFinding()]);

        md.Should().NotContain("Execution limits hit");
    }

    [Theory]
    [InlineData(ExecutionLimitCategories.ExecutionLimitToolCalls)]
    [InlineData(ExecutionLimitCategories.ExecutionLimitTokens)]
    [InlineData(ExecutionLimitCategories.ExecutionLimitWallClock)]
    [InlineData(ExecutionLimitCategories.ExecutionError)]
    public void Sarif_ExecutionLimitObservation_MapsToNoteLevelAndSuppression(string category)
    {
        var observations = new List<SkillObservation>
        {
            MakeLimitObservation(category),
        };

        var sarif = SarifOutputStrategy.BuildSarifDocument(observations);
        var doc = JsonDocument.Parse(sarif.ToJsonString());
        var result = doc.RootElement.GetProperty("runs")[0].GetProperty("results")[0];

        result.GetProperty("level").GetString().Should().Be(
            "note",
            "execution-limit observations must always render as informational, never error/warning");

        result.TryGetProperty("suppressions", out var suppressions).Should().BeTrue(
            "execution-limit observations are not security findings — they carry a suppression marker so SARIF consumers skip them in alert counts");
        suppressions.GetArrayLength().Should().Be(1);

        result.GetProperty("properties").GetProperty("category").GetString().Should().Be(category);
    }

    [Fact]
    public void Sarif_SecurityFinding_NotSuppressed()
    {
        var sarif = SarifOutputStrategy.BuildSarifDocument([MakeFinding()]);
        var doc = JsonDocument.Parse(sarif.ToJsonString());
        var result = doc.RootElement.GetProperty("runs")[0].GetProperty("results")[0];

        result.GetProperty("level").GetString().Should().Be("error", "HIGH severity → SARIF error");
        result.TryGetProperty("suppressions", out _).Should().BeFalse(
            "operator-facing findings must not be suppressed by default");
    }

    [Fact]
    public void ExecutionLimitCategories_IsExecutionLimit_RecognizesAllCategories()
    {
        ExecutionLimitCategories.IsExecutionLimit(ExecutionLimitCategories.ExecutionLimitToolCalls).Should().BeTrue();
        ExecutionLimitCategories.IsExecutionLimit(ExecutionLimitCategories.ExecutionLimitTokens).Should().BeTrue();
        ExecutionLimitCategories.IsExecutionLimit(ExecutionLimitCategories.ExecutionLimitWallClock).Should().BeTrue();
        ExecutionLimitCategories.IsExecutionLimit(ExecutionLimitCategories.ExecutionError).Should().BeTrue();
        ExecutionLimitCategories.IsExecutionLimit("injection").Should().BeFalse();
        ExecutionLimitCategories.IsExecutionLimit(null).Should().BeFalse();
    }
}
