using Xunit.Abstractions;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// p0199c docker-tier init-project coverage — deferred. The preset's
/// LoadSkills + BootstrapDispatch + BootstrapRound chain needs a populated
/// AvailableRoles dictionary, which only the real agent-smith-skills
/// catalog provides. Wiring that catalog into the docker sandbox is its
/// own work order, NOT this phase. Lands as a loud Skip-with-reason so the
/// gap is visible — silent skips are forbidden per spec.
/// </summary>
[Trait("Category", "PipelineHarness")]
[Trait("Tier", "Docker")]
public sealed class InitProjectDockerTests(ITestOutputHelper output)
{
    [Fact]
    public void Docker_InitProject_DeferredToCatalogWork_LoudSkip()
    {
        output.WriteLine(
            "DOCKER TIER NOT EXERCISED for init-project — deferred. The preset needs " +
            "a populated AvailableRoles dictionary (bootstrap-* roles) which only the " +
            "real agent-smith-skills catalog provides. Mounting that catalog into the " +
            "sandbox is its own follow-up phase; see fast-tier InitProjectTests.cs " +
            "Skip line for the same gap on the InProcess tier.");
    }
}
