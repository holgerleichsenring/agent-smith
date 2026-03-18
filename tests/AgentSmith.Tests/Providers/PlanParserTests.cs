using AgentSmith.Infrastructure.Services.Providers.Agent;
using FluentAssertions;

namespace AgentSmith.Tests.Providers;

public sealed class PlanParserTests
{
    [Fact]
    public void Parse_WithDecisions_ParsesDecisionsArray()
    {
        var json = """
            {
                "summary": "Add logging",
                "steps": [
                    { "order": 1, "description": "Add logger", "target_file": "Program.cs", "change_type": "Modify" }
                ],
                "decisions": [
                    { "category": "Architecture", "decision": "**Serilog**: structured logging required" },
                    { "category": "Tooling", "decision": "**File sink**: no external dependencies" }
                ]
            }
            """;

        var plan = PlanParser.Parse("Test", json);

        plan.Decisions.Should().HaveCount(2);
        plan.Decisions[0].Category.Should().Be("Architecture");
        plan.Decisions[0].Decision.Should().Be("**Serilog**: structured logging required");
        plan.Decisions[1].Category.Should().Be("Tooling");
        plan.Decisions[1].Decision.Should().Be("**File sink**: no external dependencies");
    }

    [Fact]
    public void Parse_WithoutDecisions_ReturnsEmptyList()
    {
        var json = """
            {
                "summary": "Simple fix",
                "steps": [
                    { "order": 1, "description": "Fix bug", "target_file": "Bug.cs", "change_type": "Modify" }
                ]
            }
            """;

        var plan = PlanParser.Parse("Test", json);

        plan.Decisions.Should().BeEmpty();
    }

    [Fact]
    public void Parse_WithEmptyDecisions_ReturnsEmptyList()
    {
        var json = """
            {
                "summary": "Simple fix",
                "steps": [
                    { "order": 1, "description": "Fix bug", "target_file": "Bug.cs", "change_type": "Modify" }
                ],
                "decisions": []
            }
            """;

        var plan = PlanParser.Parse("Test", json);

        plan.Decisions.Should().BeEmpty();
    }

    [Fact]
    public void Parse_WithMarkdownCodeBlock_StillWorks()
    {
        var json = """
            ```json
            {
                "summary": "Add feature",
                "steps": [
                    { "order": 1, "description": "Create file", "target_file": "New.cs", "change_type": "Create" }
                ],
                "decisions": [
                    { "category": "Implementation", "decision": "**Records for DTOs**: immutability by default" }
                ]
            }
            ```
            """;

        var plan = PlanParser.Parse("Test", json);

        plan.Decisions.Should().HaveCount(1);
        plan.Decisions[0].Category.Should().Be("Implementation");
    }
}
