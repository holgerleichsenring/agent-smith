using AgentSmith.Infrastructure.Core.Services;
using FluentAssertions;

namespace AgentSmith.Tests.Services.Skills;

/// <summary>A refused role names every role a skill may have — master included, which the
/// message used to leave out while accepting it.</summary>
public sealed class NewFormatSkillValidatorRoleTests
{
    [Fact]
    public void ValidateRole_Unknown_MessageListsEveryAllowedRole()
    {
        var meta = new SkillMdFrontmatter { Name = "x", Role = "wizard", Description = "d" };

        var act = () => new NewFormatSkillValidator().Validate(meta, "body", "skills/x/SKILL.md");

        act.Should().Throw<AgentSmith.Infrastructure.Core.Exceptions.SkillFormatException>()
            .WithMessage("*{producer, investigator, judge, filter, master}*got 'wizard'*");
    }
}
