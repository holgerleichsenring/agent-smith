using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Skills;
using AgentSmith.Infrastructure.Core.Services;
using FluentAssertions;

namespace AgentSmith.Tests.Services.Skills;

/// <summary>
/// p0131a: SkillBodyResolver returns Rules verbatim. Per-role body sections
/// (RoleBodies) and {{ref:X}} placeholder substitution were retired together
/// with the multi-role / References frontmatter fields.
/// </summary>
public sealed class SkillBodyResolverTests
{
    private readonly SkillBodyResolver _resolver = new();

    [Fact]
    public void ResolveBody_ReturnsRulesVerbatim()
    {
        var skill = new RoleSkillDefinition
        {
            Name = "architect",
            Rules = "Plan the implementation. Reference {{ref:ddd}} only when applicable.",
        };

        var resolved = _resolver.ResolveBody(skill, SkillRole.Lead);

        resolved.Should().Be(skill.Rules);
    }

    [Fact]
    public void ResolveBody_RepeatedCalls_CachesResult()
    {
        var skill = new RoleSkillDefinition
        {
            Name = "cache-test",
            Rules = "body",
        };

        var first = _resolver.ResolveBody(skill, SkillRole.Analyst);
        var second = _resolver.ResolveBody(skill, SkillRole.Analyst);

        ReferenceEquals(first, second).Should().BeTrue("cached body string is interned per (skill, role) tuple");
    }

    [Fact]
    public void ResolveBody_DifferentRoles_CachedSeparately()
    {
        var skill = new RoleSkillDefinition { Name = "multi", Rules = "shared body" };

        var lead = _resolver.ResolveBody(skill, SkillRole.Lead);
        var analyst = _resolver.ResolveBody(skill, SkillRole.Analyst);

        lead.Should().Be(analyst);
    }
}
