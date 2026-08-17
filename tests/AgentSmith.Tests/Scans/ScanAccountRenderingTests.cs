using AgentSmith.Application.Services.Scans;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Scans;

/// <summary>
/// p0429a: the account has been taken since p0420 and judged by the gate since p0421, and
/// the only human who ever saw it was one opening a done-phase YAML.
/// <para>
/// So a scan whose dependency audit died read exactly like a scan that audited and found
/// nothing: same finding list, same silence. This is the section both the ticket comment
/// and the pull request body embed.
/// </para>
/// </summary>
public sealed class ScanAccountRenderingTests
{
    [Fact]
    public void ScanAccount_RendersOutstandingCriteriaIntoTheTicketComment()
    {
        var pipeline = new PipelineContext();
        RunAccountLedger.Record(pipeline, [Account(
            new CriterionAccount("Known-vulnerable dependencies are identified", false, null,
                "DependencyAuditCommand failed: restore returned 401"),
            new CriterionAccount("Secrets in the working source are identified", true,
                "StaticPatternScanCommand", Mechanical: true))]);

        var section = RunAccountSection.Build(pipeline);

        section.Should().Contain("## What this scan looked for",
            "a scan accounts for what it LOOKED FOR, not for what it delivers");
        section.Should().Contain("- [ ] Known-vulnerable dependencies are identified");
        section.Should().Contain("restore returned 401",
            "the reader must see WHY a target went unanswered, not only that it did");
        section.Should().Contain("- [x] Secrets in the working source are identified");
    }

    [Fact]
    public void ScanAccount_WhenNoAccountWasTaken_RendersNothingAtAll()
    {
        RunAccountSection.Build(new PipelineContext()).Should().BeEmpty(
            "a run with no account must not add an empty heading to every ticket comment");
    }

    [Fact]
    public void RunAccount_ForAPhase_KeepsThePhaseWording()
    {
        var pipeline = new PipelineContext();
        RunAccountLedger.Record(pipeline,
            [new SpecAccount("api", [new CriterionAccount("the endpoint returns 200", true, "Api.cs")])]);

        RunAccountSection.Build(pipeline).Should().Contain("## What this run accounted for");
    }

    private static SpecAccount Account(params CriterionAccount[] criteria) =>
        new(ScanCoverageAccountant.RepoKey, criteria);
}
