using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Domain.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Specs;

/// <summary>2026-11-18-98fa: a spec's account is its master's markdown report and (2026-11-22-96c8a) every follow-up the
/// reads of its passes took off it — no rows, nothing judged per done line.</summary>
public sealed class MasterReportAccountTests
{
    private static readonly string[] Done = ["GET /orders/{id} returns 404", "Exports stream CSV"];

    [Fact]
    public void For_AFinishedSpec_CarriesTheReportAndFollowUpsAndNoRows()
    {
        var pipeline = Pipeline(Finished("## Delivered\n- the 404", "Exports stream CSV over paging"));
        AgentSmith.Application.Services.Specs.Reports.MasterReportFacts.AddFollowUps(pipeline, "s1",
            [new MasterFollowUp("Exports stream CSV over paging", "its own design")]);
        var account = MasterReportAccount.For(pipeline, ["api"]);

        account.RepoKey.Should().Be("api");
        account.Criteria.Should().BeEmpty();
        account.Report.Should().Be("## Delivered\n- the 404");
        account.FollowUps.Should().ContainSingle().Which.Goal.Should().Be("Exports stream CSV over paging");
    }

    // 2026-11-27-ecf2e (operator): a run that wrote its own spec for want of a stored approved set says so first; a missing
    // report stays missing.
    [Fact]
    public void For_ASpecWrittenWithoutAStoredApprovedSet_SaysSoFirst()
    {
        var pipeline = Pipeline(Finished("## Delivered\n- the 404", "x"));
        pipeline.Set(ContextKeys.SpecWithoutApprovedSet, true);

        MasterReportAccount.For(pipeline, ["api"]).Report.Should()
            .Be("No stored approved spec; wrote its own from the ticket.\n\n## Delivered\n- the 404");
        var unreported = Pipeline(null);
        unreported.Set(ContextKeys.SpecWithoutApprovedSet, true);
        MasterReportAccount.For(unreported, ["api"]).Report.Should().BeNull();
    }

    [Fact]
    public void For_AfterAStageRepair_TheRepairsReportStands_AndBothPassesFollowUpsAreKept()
    {
        var pipeline = Pipeline(Finished("repair report", "second"));
        pipeline.Set(ContextKeys.MasterVerificationBeforeRepair, Finished("first report", "first"));
        AgentSmith.Application.Services.Specs.Reports.MasterReportFacts.AddFollowUps(pipeline, "s1", [new MasterFollowUp("first", "its own design")]);
        AgentSmith.Application.Services.Specs.Reports.MasterReportFacts.AddFollowUps(pipeline, "s1", [new MasterFollowUp("second", "its own design")]);

        var account = MasterReportAccount.For(pipeline, ["api"]);

        account.Report.Should().Be("repair report");
        account.FollowUps!.Select(f => f.Goal).Should().Equal("first", "second");
    }

    [Fact]
    public void Missing_DoneLinesAndNoReport_SaysSo_AReportOrNoDoneList_IsNotMissing()
    {
        MasterReportAccount.Missing(Pipeline(null)).Should().Be(MasterReportAccount.NoReport).And.Contain("Retry unchanged");
        MasterReportAccount.Missing(Pipeline(new MasterVerification(null))).Should().Be(MasterReportAccount.NoReport,
            "2026-11-22-96c8a: a master that wrote no report has nobody's word on the spec");
        MasterReportAccount.Missing(Pipeline(Finished("r", "f"))).Should().BeNull();
        MasterReportAccount.Missing(new PipelineContext()).Should().BeNull();
    }

    private static PipelineContext Pipeline(MasterVerification? report)
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.CurrentSpec, new SpecDraft("s1", "goal", "spec: s1", []) { Done = Done });
        if (report is not null) pipeline.Set(ContextKeys.MasterVerification, report);
        return pipeline;
    }

    private static MasterVerification Finished(string report, string followUp) =>
        new(report,
            FollowUps: [new MasterFollowUp(followUp, "its own design")]);
}
