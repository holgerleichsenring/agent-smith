using AgentSmith.Application.Services.Activation;
using AgentSmith.Contracts.Activation;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Services.Activation;

public sealed class ActivationSkillFilterTests
{
    private readonly ActivationSkillFilter _filter = new(
        new ActivationExpressionParser(new ActivationExpressionTokenizer()),
        new ActivationEvaluator(),
        NullLogger<ActivationSkillFilter>.Instance);

    private readonly IRunStateConcepts _state =
        RunStateConceptsTestFactory.Default(new PipelineContext());

    [Fact]
    public void Filter_NullActivatesWhen_PassesThrough()
    {
        var skill = new RoleSkillDefinition { Name = "legacy", ActivatesWhen = null };
        _filter.Filter([skill], _state).Should().ContainSingle().Which.Should().BeSameAs(skill);
    }

    [Fact]
    public void Filter_EmptyActivatesWhen_PassesThrough()
    {
        var skill = new RoleSkillDefinition { Name = "blank", ActivatesWhen = "   " };
        _filter.Filter([skill], _state).Should().ContainSingle().Which.Should().BeSameAs(skill);
    }

    [Fact]
    public void Filter_ExpressionTrue_Included()
    {
        _state.SetBool("source_available", true);
        var skill = new RoleSkillDefinition { Name = "match", ActivatesWhen = "source_available" };
        _filter.Filter([skill], _state).Should().ContainSingle();
    }

    [Fact]
    public void Filter_ExpressionFalse_Excluded()
    {
        var skill = new RoleSkillDefinition { Name = "miss", ActivatesWhen = "source_available" };
        _filter.Filter([skill], _state).Should().BeEmpty();
    }

    [Fact]
    public void Filter_ParseError_ExcludesAndLogsError()
    {
        var skill = new RoleSkillDefinition { Name = "broken", ActivatesWhen = "AND OR ((" };
        _filter.Filter([skill], _state).Should().BeEmpty();
    }

    [Fact]
    public void Filter_MixedSkills_ReturnsOnlyMatching()
    {
        _state.SetBool("source_available", true);
        var legacy = new RoleSkillDefinition { Name = "legacy", ActivatesWhen = null };
        var matching = new RoleSkillDefinition { Name = "match", ActivatesWhen = "source_available" };
        var notMatching = new RoleSkillDefinition { Name = "miss", ActivatesWhen = "NOT source_available" };

        var kept = _filter.Filter([legacy, matching, notMatching], _state);

        kept.Select(s => s.Name).Should().BeEquivalentTo(["legacy", "match"]);
    }

    [Fact]
    public void Filter_ParseCachedAcrossSkills_NoDuplicateExceptions()
    {
        var a = new RoleSkillDefinition { Name = "a", ActivatesWhen = "source_available" };
        var b = new RoleSkillDefinition { Name = "b", ActivatesWhen = "source_available" };
        _state.SetBool("source_available", true);
        _filter.Filter([a, b], _state).Select(s => s.Name).Should().BeEquivalentTo(["a", "b"]);
    }
}
