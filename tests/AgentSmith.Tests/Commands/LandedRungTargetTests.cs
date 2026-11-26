using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Commands;

/// <summary>2026-11-26-5201f: a stacked pull request never opens or moves onto a prerequisite's branch that merged there while
/// the run worked — it targets the series branch, else the repository's default branch — by the claim's own rule.</summary>
public sealed class LandedRungTargetTests
{
    private readonly Mock<IPreviousAttemptReader> _attempts = new();
    private readonly RepoConnection _repo = new() { Name = "server", DefaultBranch = "develop", Url = "https://git/server" };

    private static PipelineContext Stacked(string? series = null)
    {
        var pipeline = new PipelineContext();
        pipeline.Set<object>(ContextKeys.StackedOnTicket, new Dictionary<string, string> { ["server"] = "19654" });
        if (series is not null) pipeline.Set<object>(ContextKeys.SeriesBranch, series);
        PullRequestTargets.Record(pipeline, "server", "agent-smith/19654");
        return pipeline;
    }

    private void Standing(params string[] repos) =>
        _attempts.Setup(a => a.PullRequestReposAsync("alpha", It.Is<IReadOnlyCollection<string>>(ids => ids.Contains("19654")), It.IsAny<CancellationToken>()))
            .ReturnsAsync(repos.Length == 0 ? new Dictionary<string, IReadOnlySet<string>>()
                : new Dictionary<string, IReadOnlySet<string>> { ["19654"] = repos.ToHashSet() });

    private async Task<string?> ForAsync(PipelineContext pipeline) =>
        (await new LandedRungTarget(_attempts.Object, NullLogger<LandedRungTarget>.Instance).ForAsync(pipeline, "alpha", _repo, default))?.Value;

    [Fact]
    public async Task ARungThatStillStands_StaysTheTarget()
    {
        Standing("server");
        (await ForAsync(Stacked())).Should().Be("agent-smith/19654");
    }

    [Fact]
    public async Task ARungThatMergedThere_TargetsTheSeriesBranch_ElseTheDefault()
    {
        Standing("client");
        (await ForAsync(Stacked())).Should().Be("develop");
        (await ForAsync(Stacked("agent-smith/series/s1"))).Should().Be("agent-smith/series/s1");
        Standing();
        (await ForAsync(Stacked())).Should().Be("develop");
    }

    [Fact]
    public async Task AReadThatFails_OrAnUnstackedRun_KeepsTheRecordedTarget()
    {
        _attempts.Setup(a => a.PullRequestReposAsync(It.IsAny<string>(), It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("db down"));
        (await ForAsync(Stacked())).Should().Be("agent-smith/19654");

        var plain = new PipelineContext();
        PullRequestTargets.Record(plain, "server", "agent-smith/series/s1");
        (await ForAsync(plain)).Should().Be("agent-smith/series/s1");
    }
}
