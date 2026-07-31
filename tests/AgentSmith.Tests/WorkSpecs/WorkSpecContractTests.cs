using AgentSmith.Contracts.WorkSpecs;
using FluentAssertions;

namespace AgentSmith.Tests.WorkSpecs;

// p0390 NEGATIVE test — THE SPEC CARRIES NO STEPS. The plan (p0276) owns steps and
// target files, and the ledger seeds from the plan. A future edit that adds a step
// list to the work spec would rebuild the duplication this phase resolved one level
// down, on the list that is actually load-bearing — so the absence is asserted here,
// on the type, not left to prose.
public sealed class WorkSpecContractTests
{
    private static readonly string[] StepShapedNames =
        ["step", "steps", "targetfile", "targetfiles", "target", "targets", "files", "plan"];

    [Fact]
    public void WorkSpec_CarriesNoSteps_TargetFilesRemainThePlans()
    {
        var offenders = typeof(WorkSpec).GetProperties()
            .Select(p => p.Name)
            .Where(n => StepShapedNames.Contains(n.ToLowerInvariant()))
            .ToList();

        offenders.Should().BeEmpty(
            "the work spec states WHAT must be true; steps and target files belong to the plan");
    }

    [Fact]
    public void WorkSpecConstraint_CarriesNoTargetFile_OnlyARuleAndItsSampleAnchor() =>
        typeof(WorkSpecConstraint).GetProperties().Select(p => p.Name)
            .Should().BeEquivalentTo(["Rule", "SampleAnchor"]);

    [Fact]
    public void WorkSpecKey_ForProviderAndTicket_KeepsTheTicketIdInThePath()
    {
        var key = WorkSpecKey.For("AzureDevOps", "19106");

        key.Value.Should().Be("azuredevops-19106");
        key.SpecPath.Should().Be(".agentsmith/specs/tickets/azuredevops-19106/spec.yaml");
        key.SamplesPath.Should().Be(".agentsmith/specs/tickets/azuredevops-19106/spec.md");
    }

    [Fact]
    public void WorkSpecKey_Directory_SitsUnderTheExcludedSpecsRoot() =>
        WorkSpecKey.For("github", "42").Directory.Should().StartWith(WorkSpecKey.Root + "/");

    [Fact]
    public void WorkSpecKey_MessyIdentifiers_AreSluggedIntoASafePathSegment() =>
        WorkSpecKey.For("Jira DC", "PROJ-17/a").Value.Should().Be("jira-dc-proj-17-a");

    [Fact]
    public void WorkSpecHandback_NotImplementable_IsAVerdictNotAQuestion()
    {
        new WorkSpecHandback(WorkSpecHandbackCase.NotImplementable, "why").IsVerdict
            .Should().BeTrue();
        new WorkSpecHandback(WorkSpecHandbackCase.NotUnderstood, "why").IsVerdict
            .Should().BeFalse();
    }
}
