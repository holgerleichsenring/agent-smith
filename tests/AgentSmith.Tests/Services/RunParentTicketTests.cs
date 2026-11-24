using AgentSmith.Application.Services;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Services;

/// <summary>
/// 2026-09-13-5cdf: the parent stamp 2026-09-13-a72a puts on an epic child reaches BOTH
/// consumers of the base ladder from one read — the branch the run cuts and the basis its
/// delivery is accounted against. Two readers would be two chances to disagree.
/// 2026-11-23-31dca: every answer is for one repo — a stacked run's repos are cut from their own prerequisites.
/// </summary>
public sealed class RunParentTicketTests
{
    private static PipelineContext WithTicket(params string[] labels)
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.TicketId, new TicketId("19106"));
        pipeline.Set(ContextKeys.Ticket, new Ticket(
            new TicketId("19106"), "title", "description", null, "Open", "test", labels));
        return pipeline;
    }

    [Fact]
    public void ForRepo_TheChildCarriesItsParentStamp_IsReadForEveryRepo()
    {
        var pipeline = WithTicket(FiledTicketLabels.ParentStamp("4711"));

        RunParentTicket.ForRepo(pipeline, "api").Should().Be("4711");
        RunParentTicket.LabelParent(pipeline).Should().Be("4711");
    }

    [Fact]
    public void ForRepo_ATicketThatIsNotASlice_NamesNoParent()
    {
        RunParentTicket.ForRepo(WithTicket(FiledTicketLabels.ApprovedSetStamp), "api").Should()
            .BeNull("only the parent prefix names an ancestor — cutting from anything else would be wrong");
        RunParentTicket.ForRepo(new PipelineContext(), "api").Should()
            .BeNull("a run with no ticket at all has no place in a cut");
    }

    [Fact]
    public void RunBranch_CarriesTheParentStamp_WithoutNamingItInTheBranch()
    {
        var branch = RunBranchResolver.Resolve(WithTicket(FiledTicketLabels.ParentStamp("4711")));

        branch.Should().NotBeNull();
        branch!.ParentFor("api").Should().Be("4711");
        branch.Name.Value.Should().Be("agent-smith/19106",
            "a feature segment in the name would abandon the open pull request of every "
            + "ticket that already has one");
    }

    [Fact]
    public void DeliveryBasis_ReadsTheSameStampTheCutDid()
    {
        var pipeline = WithTicket(FiledTicketLabels.ParentStamp("4711"));
        pipeline.Set(ContextKeys.RunId, "run-5cdf");
        pipeline.Set<object>(ContextKeys.StackedOnTicket, new Dictionary<string, string> { ["server"] = "41" });

        var basis = DeliveryBasis.For(pipeline, "server");

        basis.RunId.Should().Be("run-5cdf");
        basis.ParentTicketId.Should().Be(RunBranchResolver.Resolve(pipeline)!.ParentFor("server"),
            "the account resolves the base the cut used, not a base of its own");
        DeliveryBasis.For(pipeline, "client").ParentTicketId.Should().Be("4711", "client has no stacked entry");
    }

    [Fact]
    public void DeliveryBasis_ForSandbox_ReadsTheRepoTheSandboxServes()
    {
        var pipeline = WithTicket();
        pipeline.Set<object>(ContextKeys.StackedOnTicket, new Dictionary<string, string> { ["server"] = "41" });
        pipeline.Set<IReadOnlyDictionary<string, string>>(ContextKeys.SandboxRepos,
            new Dictionary<string, string> { ["server-dotnet"] = "server", ["client"] = "client" });

        DeliveryBasis.ForSandbox(pipeline, "server-dotnet").ParentTicketId.Should().Be("41");
        DeliveryBasis.ForSandbox(pipeline, "client").ParentTicketId.Should().BeNull();
    }

    [Fact]
    public void RepoOfSandbox_WithoutAMap_IsTheRunsOnlyRepo()
    {
        var pipeline = WithTicket();
        pipeline.Set<IReadOnlyList<RepoConnection>>(ContextKeys.Repos, [new RepoConnection { Name = "api" }]);

        RunParentTicket.RepoOfSandbox(pipeline, "anything").Should().Be("api");
        RunParentTicket.RepoOfSandbox(WithTicket(), "anything").Should().BeNull();
    }

    // 2026-11-17-9a39b: the claim's stamp wins over a label parent, in every shape the context carries it.
    [Fact]
    public void ForRepo_AStackedRepo_AnswersItsPrerequisite_ARepoWithoutAnEntryItsLabelParent()
    {
        var pipeline = WithTicket(FiledTicketLabels.ParentStamp("4711"));
        pipeline.Set<object>(ContextKeys.StackedOnTicket, new Dictionary<string, string> { ["server"] = "41" });

        RunParentTicket.ForRepo(pipeline, "server").Should().Be("41");
        RunParentTicket.IsStackedIn(pipeline, "server").Should().BeTrue();
        RunParentTicket.ForRepo(pipeline, "client").Should().Be("4711");
        RunParentTicket.IsStackedIn(pipeline, "client").Should().BeFalse();
        RunBranchResolver.Resolve(pipeline)!.IsStackedIn("server").Should().BeTrue();
    }

    [Fact]
    public void Stacked_AMapAfterTheQueuesRoundTrip_IsRead()
    {
        var pipeline = WithTicket();
        pipeline.Set<object>(ContextKeys.StackedOnTicket,
            System.Text.Json.JsonDocument.Parse("""{"server":"41","worker":"40"}""").RootElement);

        RunParentTicket.ForRepo(pipeline, "server").Should().Be("41");
        RunParentTicket.ForRepo(pipeline, "worker").Should().Be("40");
        RunParentTicket.ForRepo(pipeline, "client").Should().BeNull();
    }

    // A parked run stamped before 2026-11-23-31dca carries one id, which meant every repo.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Stacked_TheOldSingleIdStamp_NamesThatIdForEveryRepo(bool asJson)
    {
        var pipeline = WithTicket();
        pipeline.Set<object>(ContextKeys.StackedOnTicket,
            asJson ? System.Text.Json.JsonDocument.Parse("\"41\"").RootElement : "41");

        RunParentTicket.ForRepo(pipeline, "server").Should().Be("41");
        RunParentTicket.ForRepo(pipeline, "client").Should().Be("41");
        RunParentTicket.IsStackedIn(pipeline, "client").Should().BeTrue();
    }

    [Fact]
    public void Stacked_NoStamp_IsNotStacked()
    {
        var pipeline = WithTicket(FiledTicketLabels.ParentStamp("4711"));

        RunParentTicket.Stacked(pipeline).Should().BeNull();
        RunBranchResolver.Resolve(pipeline)!.IsStackedIn("api").Should().BeFalse();
    }

    // 2026-11-24-f9c5b
    [Fact]
    public void Series_TheStampedBranch_AnswersBelowAStackAndAboveALabelParent()
    {
        var pipeline = WithTicket(FiledTicketLabels.ParentStamp("4711"));
        pipeline.Set<object>(ContextKeys.SeriesBranch, System.Text.Json.JsonDocument.Parse("\"agent-smith/series/sr-1\"").RootElement);
        pipeline.Set<object>(ContextKeys.StackedOnTicket, new Dictionary<string, string> { ["server"] = "41" });

        RunParentTicket.ForRepo(pipeline, "server").Should().Be("41");
        RunParentTicket.ForRepo(pipeline, "client").Should().Be("agent-smith/series/sr-1");
        var run = RunBranchResolver.Resolve(pipeline)!;
        run.ParentFor("client").Should().Be("agent-smith/series/sr-1");
        run.IsSeriesIn("client").Should().BeTrue();
        run.IsSeriesIn("server").Should().BeFalse();
    }

    // 2026-11-24-f9c5b
    [Theory]
    [InlineData("agent-smith/series/sr-1", "agent-smith/series/sr-1")]
    [InlineData("4711", "agent-smith/4711")]
    public void RungOf_ASeriesBranchIsItself_ATicketIsItsBranch(string parent, string rung) =>
        AgentSmith.Application.Services.TicketBranchNamer.RungOf(parent).Value.Should().Be(rung);

    // 2026-11-24-f9c5b
    [Fact]
    public void Series_IsNeverReadAsATicketsBranch()
    {
        var branch = AgentSmith.Application.Services.TicketBranchNamer.Series("sr-1").Value;

        branch.Should().Be("agent-smith/series/sr-1");
        AgentSmith.Application.Services.TicketBranchNamer.TicketOf(branch).Should().BeNull();
        AgentSmith.Application.Services.TicketBranchNamer.IsSeries("refs/heads/" + branch).Should().BeTrue();
        AgentSmith.Application.Services.TicketBranchNamer.IsSeries("agent-smith/41").Should().BeFalse();
    }
}
