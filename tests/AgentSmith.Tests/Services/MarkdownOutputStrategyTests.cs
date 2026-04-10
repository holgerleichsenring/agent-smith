using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Services.Output;
using FluentAssertions;

namespace AgentSmith.Tests.Services;

public sealed class MarkdownOutputStrategyTests
{
    [Fact]
    public void BuildMarkdown_NoFindings_SaysNoIssues()
    {
        var result = MarkdownOutputStrategy.BuildMarkdown([]);

        result.Should().Contain("No issues found");
        result.Should().NotContain("| Severity |");
    }

    [Fact]
    public void BuildMarkdown_WithFindings_RendersTable()
    {
        var findings = new List<Finding>
        {
            new("HIGH", "src/Api/UserController.cs", 47, 52, "SQL injection", "Desc", 9),
            new("MEDIUM", "src/Auth/TokenService.cs", 23, null, "JWT secret", "Desc", 8),
            new("LOW", "src/Config/DbConfig.cs", 8, null, "Logged secret", "Desc", 8),
        };

        var result = MarkdownOutputStrategy.BuildMarkdown(findings);

        result.Should().Contain("## Agent Smith Security Review");
        result.Should().Contain("Found 3 issues (1 HIGH, 1 MEDIUM, 1 LOW)");
        result.Should().Contain("| Severity | Location | Issue |");
        result.Should().Contain("src/Api/UserController.cs:47");
        result.Should().Contain("SQL injection");
    }
}
