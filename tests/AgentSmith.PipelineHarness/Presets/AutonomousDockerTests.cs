using Xunit.Abstractions;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// p0199c docker-tier autonomous coverage — deferred. Same blocker as
/// init-project: Triage + SkillRound demand non-empty AvailableRoles
/// (autonomous-* skills) loaded by LoadSkills from the real catalog.
/// Honest scope-slicing: loud Skip naming the next follow-up, not a
/// silent green.
/// </summary>
[Trait("Category", "PipelineHarness")]
[Trait("Tier", "Docker")]
public sealed class AutonomousDockerTests(ITestOutputHelper output)
{
    [Fact]
    public void Docker_Autonomous_DeferredToCatalogWork_LoudSkip()
    {
        output.WriteLine(
            "DOCKER TIER NOT EXERCISED for autonomous — deferred. The preset's Triage " +
            "+ SkillRound chain requires the autonomous-* skill set in AvailableRoles, " +
            "loaded by LoadSkills from the real agent-smith-skills catalog. Same gap " +
            "as the fast-tier AutonomousTests.cs Skip line; closing it needs the " +
            "skill catalog mounted into the sandbox (separate follow-up).");
    }
}
