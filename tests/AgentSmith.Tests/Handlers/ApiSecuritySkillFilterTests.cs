using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Handlers;

public sealed class ApiSecuritySkillFilterTests
{
    private static IReadOnlyList<RoleSkillDefinition> AllSkills() =>
    [
        Role("recon-analyst"),
        Role("anonymous-attacker"),
        Role("api-design-auditor"),
        Role("auth-tester"),
        Role("dast-analyst"),
        Role("idor-prober"),
        Role("low-privilege-attacker"),
        Role("input-abuser"),
        Role("response-analyst"),
        Role("auth-config-reviewer"),
        Role("ownership-checker"),
        Role("upload-validator-reviewer"),
        Role("false-positive-filter"),
        Role("chain-analyst"),
    ];

    [Fact]
    public void PassiveNoSource_FourSkills()
    {
        var filtered = new ApiSecuritySkillFilter().Filter(AllSkills(), activeMode: false, sourceAvailable: false);
        filtered.Select(r => r.Name).Should().BeEquivalentTo(
            "recon-analyst", "anonymous-attacker", "false-positive-filter", "chain-analyst");
    }

    [Fact]
    public void PassiveWithSource_AddsCodeSkillsKeepsContributors()
    {
        var filtered = new ApiSecuritySkillFilter().Filter(AllSkills(), activeMode: false, sourceAvailable: true);
        var names = filtered.Select(r => r.Name).ToList();
        names.Should().Contain(["auth-config-reviewer", "ownership-checker", "upload-validator-reviewer"]);
        names.Should().Contain(["recon-analyst", "anonymous-attacker"]);
    }

    [Fact]
    public void ActiveWithSource_FullPool()
    {
        var filtered = new ApiSecuritySkillFilter().Filter(AllSkills(), activeMode: true, sourceAvailable: true);
        filtered.Should().HaveCount(AllSkills().Count);
    }

    [Fact]
    public void ActiveNoSource_DropsCodeSkillsKeepsActiveAttackers()
    {
        var filtered = new ApiSecuritySkillFilter().Filter(AllSkills(), activeMode: true, sourceAvailable: false);
        var names = filtered.Select(r => r.Name).ToList();
        names.Should().NotContain("auth-config-reviewer");
        names.Should().NotContain("ownership-checker");
        names.Should().NotContain("upload-validator-reviewer");
        names.Should().Contain(["idor-prober", "low-privilege-attacker", "input-abuser"]);
    }

    private static RoleSkillDefinition Role(string name) =>
        new() { Name = name, DisplayName = name, Emoji = "" };
}
