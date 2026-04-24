using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Infrastructure.Core.Services;
using FluentAssertions;

namespace AgentSmith.Tests.Services;

public sealed class SkillOrchestrationParserTests
{
    [Fact]
    public void GateListRole_MissingInputCategories_Throws()
    {
        var content = """
            ## orchestration
            role: gate
            output: list
            """;

        Action act = () => SkillOrchestrationParser.Parse(content);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*must declare input_categories*");
    }

    [Fact]
    public void GateListRole_EmptyInputCategories_Throws()
    {
        var content = """
            ## orchestration
            role: gate
            output: list
            input_categories: ,
            """;

        Action act = () => SkillOrchestrationParser.Parse(content);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*must declare input_categories*");
    }

    [Fact]
    public void GateListRole_WildcardInputCategories_Parses()
    {
        var content = """
            ## orchestration
            role: gate
            output: list
            input_categories: *
            """;

        var result = SkillOrchestrationParser.Parse(content);

        result.Should().NotBeNull();
        result!.Role.Should().Be(SkillRole.Gate);
        result.InputCategories.Should().ContainSingle().Which.Should().Be("*");
    }

    [Fact]
    public void GateListRole_ExplicitInputCategories_Parses()
    {
        var content = """
            ## orchestration
            role: gate
            output: list
            input_categories: secrets, injection, design
            """;

        var result = SkillOrchestrationParser.Parse(content);

        result.Should().NotBeNull();
        result!.InputCategories.Should().BeEquivalentTo(new[] { "secrets", "injection", "design" });
    }

    [Fact]
    public void GateListRole_WildcardMixedWithCategories_Throws()
    {
        var content = """
            ## orchestration
            role: gate
            output: list
            input_categories: *, secrets
            """;

        Action act = () => SkillOrchestrationParser.Parse(content);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*cannot mix*wildcard*");
    }

    [Fact]
    public void GateVerdictRole_EmptyInputCategories_Parses()
    {
        // Verdict gates don't filter findings by category — empty is acceptable
        var content = """
            ## orchestration
            role: gate
            output: verdict
            """;

        var result = SkillOrchestrationParser.Parse(content);

        result.Should().NotBeNull();
        result!.Output.Should().Be(SkillOutputType.Verdict);
        result.InputCategories.Should().BeEmpty();
    }

    [Fact]
    public void ContributorRole_EmptyInputCategories_Parses()
    {
        var content = """
            ## orchestration
            role: contributor
            output: artifact
            """;

        var result = SkillOrchestrationParser.Parse(content);

        result.Should().NotBeNull();
        result!.Role.Should().Be(SkillRole.Contributor);
        result.InputCategories.Should().BeEmpty();
    }

    [Fact]
    public void NoOrchestrationSection_ReturnsNull()
    {
        var content = """
            ## description
            Something else.
            """;

        var result = SkillOrchestrationParser.Parse(content);

        result.Should().BeNull();
    }
}
