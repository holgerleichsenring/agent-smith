using AgentSmith.Contracts.Models;
using AgentSmith.Infrastructure.Services.Output;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;

namespace AgentSmith.Tests.Services;

public sealed class MarkdownOutputStrategyTests
{
    [Fact]
    public void BuildMarkdown_NoObservations_SaysNoIssues()
    {
        var result = MarkdownOutputStrategy.BuildMarkdown([]);

        result.Should().Contain("No issues found");
        result.Should().NotContain("| Severity |");
    }

    [Fact]
    public void BuildMarkdown_WithObservations_RendersSections()
    {
        var observations = new List<SkillObservation>
        {
            ObservationFactory.Make("HIGH", "src/Api/UserController.cs", 47, "SQL injection", "Desc", 90),
            ObservationFactory.Make("MEDIUM", "src/Auth/TokenService.cs", 23, "JWT secret", "Desc", 80),
            ObservationFactory.Make("LOW", "src/Config/DbConfig.cs", 8, "Logged secret", "Desc", 80),
        };

        var result = MarkdownOutputStrategy.BuildMarkdown(observations);

        result.Should().Contain("## Agent Smith Security Review");
        result.Should().Contain("Found **3** issues (1 high, 1 medium, 1 low, 0 info)");
        result.Should().Contain("### 🟠 HIGH: SQL injection");
        result.Should().Contain("**Location:** `src/Api/UserController.cs:47`");
        result.Should().Contain("Desc");
    }
}
