using AgentSmith.PipelineHarness.Composition;
using FluentAssertions;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// p0199 autonomous coverage. Deferred to p0199b — same LLM-coupling
/// gap as init-project. Triage refuses to run without AvailableRoles
/// holding the autonomous-* skill catalog; loading a real catalog into
/// the harness is the work order for p0199b. Honest scope-slicing per
/// spec: a single fact remains so the deferral is visible.
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class AutonomousTests
{
    [Fact(Skip = "Deferred to p0199b: Triage demands a non-empty AvailableRoles " +
        "(populated by LoadSkills from a real catalog). With the harness's " +
        "empty skills root the autonomous-* roles never load and Triage " +
        "fails with 'No skills loaded'. Either seed AvailableRoles with " +
        "the autonomous catalog OR point the harness at the agent-smith-" +
        "skills v3.5.0 tree — both are p0199b work.")]
    public Task Autonomous_RealHandlerChain_PipelineGreen() => Task.CompletedTask;
}
