using AgentSmith.Application.Services.SkillRounds;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using FluentAssertions;

namespace AgentSmith.Tests.SkillRounds;

public sealed class FilterRoundToolPolicyTests
{
    [Fact]
    public void FilterRoundToolPolicy_AnyInput_ReturnsEmpty()
    {
        var policy = new FilterRoundToolPolicy();

        var emptyContext = policy.GetTools(new RoleSkillDefinition { Name = "x", Role = "filter" }, new PipelineContext());
        emptyContext.Should().BeEmpty();

        var loadedRole = new RoleSkillDefinition
        {
            Name = "x",
            Role = "filter",
            InvestigatorMode = "survey", // ignored
        };
        var loadedContext = new PipelineContext();
        loadedContext.Set(ContextKeys.PipelineName, "api-security-scan");
        policy.GetTools(loadedRole, loadedContext).Should().BeEmpty();
    }
}
