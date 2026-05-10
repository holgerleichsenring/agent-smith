using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Core.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Services.Skills;

public sealed class SkillMdParserMergeNewFieldsTests : IDisposable
{
    private readonly string _skillDir;

    public SkillMdParserMergeNewFieldsTests()
    {
        _skillDir = Path.Combine(Path.GetTempPath(), "agentsmith-merge-new-" + Guid.NewGuid());
        Directory.CreateDirectory(_skillDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_skillDir)) Directory.Delete(_skillDir, recursive: true);
    }

    [Fact]
    public void MergeFrontmatter_OverrideSetsCategory_OverrideWins()
    {
        WriteBase("""
            ---
            name: investigator
            description: "base"
            role: investigator
            investigator_mode: verify_hint
            category: auth
            output_schema: observation
            activates_when: "true"
            ---
            base body
            """);
        WriteOverride("""
            ---
            name: investigator
            category: injection
            ---
            override body
            """);

        var role = NewParser("openai").Parse(_skillDir)!;
        role.Category.Should().Be("injection");
    }

    [Fact]
    public void MergeFrontmatter_OverrideOmitsCategory_BaseWins()
    {
        WriteBase("""
            ---
            name: investigator
            description: "base"
            role: investigator
            investigator_mode: verify_hint
            category: auth
            output_schema: observation
            activates_when: "true"
            ---
            base body
            """);
        WriteOverride("""
            ---
            name: investigator
            ---
            override body
            """);

        var role = NewParser("openai").Parse(_skillDir)!;
        role.Category.Should().Be("auth");
    }

    [Fact]
    public void MergeFrontmatter_OverrideSetsSurveyScope_OverrideListReplacesBase()
    {
        WriteBase("""
            ---
            name: surveyor
            description: "base"
            role: investigator
            investigator_mode: survey
            survey_scope: ["src/**/*.cs"]
            output_schema: observation
            activates_when: "true"
            ---
            base body
            """);
        WriteOverride("""
            ---
            name: surveyor
            survey_scope: ["tests/**/*.cs"]
            ---
            override body
            """);

        var role = NewParser("openai").Parse(_skillDir)!;
        role.SurveyScope.Should().BeEquivalentTo(new[] { "tests/**/*.cs" });
    }

    [Fact]
    public void MergeFrontmatter_OverrideSetsLoopFalse_BaseTrueOverridden()
    {
        WriteBase("""
            ---
            name: looper
            description: "base"
            role: producer
            output_schema: plan
            activates_when: "true"
            loop: true
            ---
            base body
            """);
        WriteOverride("""
            ---
            name: looper
            loop: false
            ---
            override body
            """);

        var role = NewParser("openai").Parse(_skillDir)!;
        role.Loop.Should().Be(false);
    }

    [Fact]
    public void MergeFrontmatter_OverrideOmitsAllNewFields_BaseValuesPreserved()
    {
        WriteBase("""
            ---
            name: complete
            description: "base"
            role: investigator
            investigator_mode: survey
            survey_scope: ["src/**"]
            scope_hint: "controllers"
            output_schema: observation
            activates_when: "true"
            loop: true
            ---
            base body
            """);
        WriteOverride("""
            ---
            name: complete
            ---
            override body
            """);

        var role = NewParser("openai").Parse(_skillDir)!;
        role.Role.Should().Be("investigator");
        role.InvestigatorMode.Should().Be("survey");
        role.SurveyScope.Should().BeEquivalentTo(new[] { "src/**" });
        role.ScopeHint.Should().Be("controllers");
        role.OutputSchema.Should().Be("observation");
        role.Loop.Should().Be(true);
    }

    private void WriteBase(string content) =>
        File.WriteAllText(Path.Combine(_skillDir, "SKILL.md"), content);

    private void WriteOverride(string content) =>
        File.WriteAllText(Path.Combine(_skillDir, "SKILL.openai.md"), content);

    private static SkillMdParser NewParser(string activeProvider)
    {
        var providerMock = new Mock<IActiveProviderResolver>();
        providerMock.Setup(x => x.GetActiveProvider()).Returns(activeProvider);
        var resolver = new ProviderOverrideResolver(providerMock.Object);
        return new SkillMdParser(resolver, NullLogger.Instance);
    }
}
