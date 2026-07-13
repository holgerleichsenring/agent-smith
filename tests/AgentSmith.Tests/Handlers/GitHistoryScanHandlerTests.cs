using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Handlers;

/// <summary>
/// p0333b: a git-history secret's observation severity must stay High+ so
/// MergeMasterFindings never drops it. History-only (deleted, still retrievable)
/// is Critical; still-in-working-tree is High. The pre-fix Medium for history-only
/// fell off the merge's High+ promotion cliff and silently lost the finding.
/// </summary>
public sealed class GitHistoryScanHandlerTests
{
    [Fact]
    public async Task Execute_HistoryOnlySecret_EmittedAsCriticalObservation()
    {
        var pipeline = await RunWith(
            Finding("removed-token", stillInTree: false));

        var obs = Observations(pipeline).Should().ContainSingle().Subject;
        obs.Severity.Should().Be(ObservationSeverity.Critical,
            "a deleted-but-in-history secret is the higher risk and must survive the merge's High+ filter");
        obs.Role.Should().Be("git-history-scanner");
    }

    [Fact]
    public async Task Execute_SecretStillInWorkingTree_EmittedAsHighObservation()
    {
        var pipeline = await RunWith(
            Finding("live-token", stillInTree: true));

        Observations(pipeline).Should().ContainSingle()
            .Which.Severity.Should().Be(ObservationSeverity.High);
    }

    private static HistoryFinding Finding(string title, bool stillInTree) =>
        new(PatternId: "generic-api-key", Severity: "high", CommitHash: "abcdef1234567",
            File: "src/App/secrets.cs", Line: 3, Title: title, Description: "matched a key-shaped token",
            MatchedText: null, StillInWorkingTree: stillInTree);

    private static async Task<PipelineContext> RunWith(params HistoryFinding[] findings)
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.Repository, new Repository(new BranchName("main"), "https://example/repo.git"));
        pipeline.Set<ISandbox>(ContextKeys.Sandbox, new StubSandbox());

        var handler = new GitHistoryScanHandler(
            new StubGitHistoryScanner(new GitHistoryScanResult(findings, CommitsScanned: 10, DurationMilliseconds: 5)),
            new StubSandboxFileReaderFactory(),
            NullLogger<GitHistoryScanHandler>.Instance);

        await handler.ExecuteAsync(new GitHistoryScanContext(pipeline), CancellationToken.None);
        return pipeline;
    }

    private static List<SkillObservation> Observations(PipelineContext pipeline) =>
        pipeline.TryGet<List<SkillObservation>>(ContextKeys.SkillObservations, out var obs) && obs is not null
            ? obs
            : [];

    private sealed class StubGitHistoryScanner(GitHistoryScanResult result) : IGitHistoryScanner
    {
        public Task<GitHistoryScanResult> ScanAsync(
            ISandbox sandbox, ISandboxFileReader reader, CancellationToken cancellationToken) =>
            Task.FromResult(result);
    }
}
