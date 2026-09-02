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

    // The guard reads the COMMAND LIST, not where the run died, so failing the first
    // step keeps the mocked collaborators out of it while the list stays the real preset.
    // The legacy handler names, which no preset carries any more but the skill-manager and
    // autonomous paths still compose by hand.
    [Fact]
    public void AComposedListNamingACodeModifyingHandler_StillIntendsToChangeCode() =>
        WorkBranchPersistPolicy.IntendedToChangeCode(
            [CommandNames.AgenticExecute, CommandNames.WriteRunResult]).Should().BeTrue();

    [Fact]
    public void AComposedListThatDeliversFindings_NeverIntendsToChangeCode() =>
        WorkBranchPersistPolicy.IntendedToChangeCode(
            [CommandNames.AgenticMaster, CommandNames.DeliverFindings, CommandNames.CommitAndPR])
            .Should().BeFalse();

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
