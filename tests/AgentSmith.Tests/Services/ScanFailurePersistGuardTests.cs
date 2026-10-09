using AgentSmith.Application.Services;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.Services;

/// <summary>
/// 2026-09-02-de87: the persist guard against the REAL presets.
/// <para>
/// The existing guard tests hand the executor a hand-written command list that omits
/// AgenticMaster, so they passed while the shipped security-scan preset — which carries it —
/// persisted a work branch on failure and staged the tree it had been reading.
/// </para>
/// </summary>
public sealed class ScanFailurePersistGuardTests
{
    [Fact]
    public async Task FailedSecurityScan_PersistsNoWorkBranch()
    {
        var h = await RunFailing(PipelinePresets.SecurityScan, WithRepository());
        AssertNoPersist(h);
    }

    [Fact]
    public async Task FailedApiSecurityScan_PersistsNoWorkBranch()
    {
        var h = await RunFailing(PipelinePresets.ApiSecurityScan, WithRepository());
        AssertNoPersist(h);
    }

    [Fact]
    public async Task FailedCodingRun_StillPersistsItsWorkBranch()
    {
        var h = await RunFailing(PipelinePresets.Code, WithRepository());
        h.FactoryMock.Verify(f => f.Create(
            It.Is<PipelineCommand>(c => c.Name == CommandNames.PersistWorkBranch),
            It.IsAny<ResolvedProject>(), It.IsAny<PipelineContext>()), Times.Once);
    }

    [Fact]
    public async Task FailedRunWithNoRepository_PersistsNothing()
    {
        var h = await RunFailing(PipelinePresets.Code, WithRepos(new PipelineContext()));
        AssertNoPersist(h);
    }

    // 2026-10-09-af10: a failed pr-review pushed a WIP commit onto the PR's own branch, whose new
    // head the PR sweep reviewed again — twenty times in half an hour.
    [Fact]
    public async Task FailedPrReview_PersistsNoWorkBranch()
    {
        var h = await RunFailing(PipelinePresets.PrReview, WithRepository());
        AssertNoPersist(h);
    }

    // One row per shipped preset: only a run that COMMITS its work and delivers no opinion persists.
    [Theory]
    [InlineData(PipelinePresets.CodeName, true)]
    [InlineData("mad-discussion", true)]
    [InlineData("pr-review", false)]
    [InlineData("security-scan", false)]
    [InlineData("api-security-scan", false)]
    [InlineData("legal-analysis", false)]
    [InlineData(PipelinePresets.SpecDialogName, false)]
    [InlineData("init-project", false)]
    public void PersistPolicy_PerPreset_MatchesTruthTable(string preset, bool persists) =>
        WorkBranchPersistPolicy.IntendedToChangeCode(PipelinePresets.TryResolve(preset)!).Should().Be(persists);

    [Fact]
    public void PersistPolicy_TruthTable_CoversEveryPreset() =>
        PipelinePresets.Names.Should().BeEquivalentTo([PipelinePresets.CodeName, "mad-discussion", "pr-review", "security-scan",
            "api-security-scan", "legal-analysis", PipelinePresets.SpecDialogName, "init-project"]);

    // The webhook path: a PR push routes to the resolver's default pipeline, which must never persist.
    [Fact]
    public void PrReviewRouteDefault_NeverPersists() =>
        WorkBranchPersistPolicy.IntendedToChangeCode(
            PipelinePresets.TryResolve(AgentSmith.Server.Services.Webhooks.PrReviewRouteResolver.DefaultPipeline)!)
            .Should().BeFalse();

    [Fact]
    public void AMasterWithoutACommit_NeverIntendsToChangeCode() =>
        WorkBranchPersistPolicy.IntendedToChangeCode(
            [CommandNames.AgenticMaster, CommandNames.WriteRunResult]).Should().BeFalse();

    [Fact]
    public void AComposedListThatDeliversFindings_NeverIntendsToChangeCode() =>
        WorkBranchPersistPolicy.IntendedToChangeCode(
            [CommandNames.AgenticMaster, CommandNames.DeliverFindings, CommandNames.CommitAndPR])
            .Should().BeFalse();

    [Fact]
    public void AComposedListThatPostsPrComments_NeverIntendsToChangeCode() =>
        WorkBranchPersistPolicy.IntendedToChangeCode(
            [CommandNames.PostPrComments, CommandNames.CommitAndPR]).Should().BeFalse();

    private static async Task<PipelineExecutorTestBuilder> RunFailing(
        IReadOnlyList<string> preset, PipelineContext pipeline)
    {
        var h = new PipelineExecutorTestBuilder();
        var commands = preset.ToArray();
        h.FactoryMock.Setup(f => f.Create(
            It.Is<PipelineCommand>(c => c.Name == commands[0]),
            It.IsAny<ResolvedProject>(), It.IsAny<PipelineContext>()))
            .Throws(new Exception($"{commands[0]} crashed for test"));

        var result = await h.Sut.ExecuteAsync(
            commands, ProjectWithImage(), pipeline, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        return h;
    }

    private static void AssertNoPersist(PipelineExecutorTestBuilder h) =>
        h.FactoryMock.Verify(f => f.Create(
            It.Is<PipelineCommand>(c => c.Name == CommandNames.PersistWorkBranch),
            It.IsAny<ResolvedProject>(), It.IsAny<PipelineContext>()), Times.Never);

    private static ResolvedProject ProjectWithImage() =>
        new() { Sandbox = new SandboxConfig { ToolchainImage = "dotnet8" } };

    private static PipelineContext WithRepository()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.Repository,
            new Repository(new BranchName("main"), "https://example.com/repo.git"));
        return WithRepos(pipeline);
    }

    private static PipelineContext WithRepos(PipelineContext pipeline)
    {
        pipeline.Set<IReadOnlyList<RepoConnection>>(ContextKeys.Repos, new[] { new RepoConnection() });
        return pipeline;
    }
}
