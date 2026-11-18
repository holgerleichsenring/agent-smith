using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Domain.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Specs;

/// <summary>2026-11-18-98fa: a spec's account is its master's markdown report and the follow-ups finish_spec carried —
/// no rows, nothing judged per done line.</summary>
public sealed class MasterReportAccountTests
{
    private static readonly string[] Done = ["GET /orders/{id} returns 404", "Exports stream CSV"];

    [Fact]
    public void For_AFinishedSpec_CarriesTheReportAndFollowUpsAndNoRows()
    {
        var account = MasterReportAccount.For(Pipeline(Finished("## Delivered\n- the 404", "Exports stream CSV over paging")), ["api"]);

        account.RepoKey.Should().Be("api");
        account.Criteria.Should().BeEmpty();
        account.Report.Should().Be("## Delivered\n- the 404");
        account.FollowUps.Should().ContainSingle().Which.Goal.Should().Be("Exports stream CSV over paging");
    }

    [Fact]
    public void For_AfterAStageRepair_TheRepairsReportStands_AndBothPassesFollowUpsAreKept()
    {
        var pipeline = Pipeline(Finished("repair report", "second"));
        pipeline.Set(ContextKeys.MasterVerificationBeforeRepair, Finished("first report", "first"));

        var account = MasterReportAccount.For(pipeline, ["api"]);

        account.Report.Should().Be("repair report");
        account.FollowUps!.Select(f => f.Goal).Should().Equal("first", "second");
    }

    [Fact]
    public void Missing_DoneLinesAndNoFinishSpec_SaysSo_AFinishOrNoDoneList_IsNotMissing()
    {
        MasterReportAccount.Missing(Pipeline(null)).Should().Be(MasterReportAccount.NoFinish).And.Contain("without finish_spec");
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
        new(VerificationStatus.Green, true, true, true, true, "s",
            FollowUps: [new MasterFollowUp(followUp, "its own design")], Report: report);
}
