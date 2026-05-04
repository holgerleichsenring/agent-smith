using AgentSmith.Application.Services.Triage;
using AgentSmith.Contracts.Models.Skills;
using FluentAssertions;

namespace AgentSmith.Tests.Triage;

public sealed class TriageRationaleParserTests
{
    private readonly TriageRationaleParser _sut = new();

    [Fact]
    public void Parse_TokenGrammar_ProducesStructuredEntries()
    {
        const string rationale = "lead=architect:auth-port;analyst=tester:has-tests;-dba:no-db-changes;";

        var entries = _sut.Parse(rationale);

        entries.Should().HaveCount(3);
        entries[0].Should().Be(new RationaleEntry(SkillRole.Lead, "architect", "auth-port", false));
        entries[1].Should().Be(new RationaleEntry(SkillRole.Analyst, "tester", "has-tests", false));
        entries[2].Should().Be(new RationaleEntry(null, "dba", "no-db-changes", true));
    }

    [Fact]
    public void Parse_EmptyString_ReturnsEmptyList()
    {
        _sut.Parse("").Should().BeEmpty();
        _sut.Parse("   ").Should().BeEmpty();
    }

    [Fact]
    public void Parse_MalformedTokensSilentlyDropped_ValidTokensReturned()
    {
        var entries = _sut.Parse("lead=architect:keep;not-a-token;reviewer=tester:also-keep;");

        entries.Should().HaveCount(2);
        entries[0].Skill.Should().Be("architect");
        entries[1].Skill.Should().Be("tester");
    }
}
