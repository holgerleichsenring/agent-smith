using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Domain.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Specs;

/// <summary>2026-11-18-c8e2c: a spec's account is its master's report, its dispositions paired with the done lines.</summary>
public sealed class MasterReportAccountTests
{
    private static readonly string[] Done = ["GET /orders/{id} returns 404", "Exports stream CSV", "Legacy DTO stays", "Logs once"];

    [Fact]
    public void For_AReport_MapsEachLineByNameWithItsWhyAndCarriesTheRest()
    {
        var pipeline = Pipeline(Report(
            ("Exports stream CSV", AcceptanceStatus.NotMet, "needs paging; follow-up", null),
            ("GET /orders/{id} returns 404", AcceptanceStatus.Met, "the controller", ["src/Orders.cs"]),
            ("Legacy DTO stays", AcceptanceStatus.NotApplicable, "no DTO in this repository", null)));

        var account = MasterReportAccount.For(pipeline, ["api"]);

        account.RepoKey.Should().Be("api");
        account.Criteria.Select(c => (c.Criterion, c.Disposition, c.Citation, c.Note)).Should().Equal(
            ("GET /orders/{id} returns 404", AccountDisposition.Satisfied, "src/Orders.cs", "the controller"),
            ("Exports stream CSV", AccountDisposition.NotSatisfied, null, "needs paging; follow-up"),
            ("Legacy DTO stays", AccountDisposition.NotApplicable, null, "no DTO in this repository"),
            ("Logs once", AccountDisposition.NotSatisfied, null, MasterReportAccount.NoDisposition));
        account.FixedInPassing.Should().Equal("a null check");
        account.FollowUps.Should().ContainSingle().Which.Goal.Should().Be("Exports stream CSV over paging");
    }

    [Fact]
    public void For_AfterAStageRepairThatRestatesOneLine_TheOthersKeepTheFirstPass()
    {
        var pipeline = Pipeline(Report(("Logs once", AcceptanceStatus.Met, "fixed the build", ["src/Log.cs"])));
        pipeline.Set(ContextKeys.MasterVerificationBeforeRepair,
            Report(("GET /orders/{id} returns 404", AcceptanceStatus.Met, "the controller", ["src/Orders.cs"])));

        var account = MasterReportAccount.For(pipeline, ["api"]);

        account.Criteria[0].IsSatisfied.Should().BeTrue("the repair did not restate it, so the first pass's disposition stands");
        account.Criteria[3].Citation.Should().Be("src/Log.cs");
    }

    [Fact]
    public void Missing_DoneLinesAndNoReport_SaysSo_AReportOrNoDoneList_IsNotMissing()
    {
        MasterReportAccount.Missing(Pipeline(null)).Should().Contain("without its report");
        MasterReportAccount.Missing(Pipeline(Report())).Should().BeNull();
        MasterReportAccount.Missing(new PipelineContext()).Should().BeNull();
    }

    private static PipelineContext Pipeline(MasterVerification? report)
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.CurrentSpec, new SpecDraft("s1", "goal", "spec: s1", []) { Done = Done });
        if (report is not null) pipeline.Set(ContextKeys.MasterVerification, report);
        return pipeline;
    }

    private static MasterVerification Report(params (string Criterion, AcceptanceStatus Status, string Why, string[]? Cites)[] lines) =>
        new(VerificationStatus.Green, true, true, true, true, "s",
            AcceptanceDispositions: [.. lines.Select(l => new AcceptanceDisposition(l.Criterion, l.Status, l.Why) { Citations = l.Cites })],
            FixedInPassing: ["a null check"],
            FollowUps: [new MasterFollowUp("Exports stream CSV over paging", "paging is its own design")]);
}
