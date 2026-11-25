using System.Reflection;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.PipelineHarness.Composition;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// 2026-11-17-023fa: the code and merge-green presets through the real composition. 2026-11-25-9992i: a green code run merges
/// nothing under any review — end and each leave its pull request for a person, none waits for CI — and the merge-green run
/// merges under the host's branch protection (auto-merge), whose rules decide.
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class MergeByReviewTests
{
    [Theory]
    [InlineData(ReviewMode.End)]
    [InlineData(ReviewMode.Each)]
    [InlineData(ReviewMode.None)]
    public async Task GreenRun_MergesNothing_UnderEveryReview(ReviewMode review)
    {
        var merges = new List<BranchProtection>();
        var result = await RunGreenAsync(merges, review);

        result.IsSuccess.Should().BeTrue(result.Message);
        merges.Should().BeEmpty();
    }

    [Fact]
    public async Task MergeGreenRun_MergesTheSeededPullRequest()
    {
        var merges = new List<BranchProtection>();
        await using var harness = RealCompositionHarness.Build(FixturePaths.For(FixturePaths.Default), services =>
        {
            services.RemoveAll<ISourceProviderFactory>();
            services.AddSingleton<ISourceProviderFactory>(new RecordingFactory(new StubSourceProviderFactory(), merges));
        });
        var runner = new PipelineRunner(harness.Services) { Review = ReviewMode.None };
        runner.RequestContext[AgentSmith.Contracts.Commands.ContextKeys.CiGreenMerge] = new AgentSmith.Contracts.Runs.CiGreenMergeRequest(
            "42", "attempt-1", "agent-smith/42", [new AgentSmith.Contracts.Runs.CiGreenPullRequest("primary", "https://stub.test/primary/pull/1")]);

        var result = await runner.RunAsync(AgentSmith.Contracts.Commands.PipelinePresets.MergeGreenName);

        result.IsSuccess.Should().BeTrue(result.Message);
        merges.Should().Equal(BranchProtection.Respect);
    }

    // 2026-11-23-31dce: a merge a person asked for from the series page runs under review: end and hands back its outcome.
    [Fact]
    public async Task MergeGreenRun_AskedByAPerson_UnderEnd_MergesAndFillsTheOutcomeSlot()
    {
        var merges = new List<BranchProtection>();
        await using var harness = RealCompositionHarness.Build(FixturePaths.For(FixturePaths.Default), services =>
        {
            services.RemoveAll<ISourceProviderFactory>();
            services.AddSingleton<ISourceProviderFactory>(new RecordingFactory(new StubSourceProviderFactory(), merges));
        });
        var slot = new AgentSmith.Application.Services.TicketActs.ActSlot<IReadOnlyList<AgentSmith.Application.Models.PullRequestMergeLine>>();
        var runner = new PipelineRunner(harness.Services);
        runner.RequestContext[AgentSmith.Contracts.Commands.ContextKeys.CiGreenMerge] = new AgentSmith.Contracts.Runs.CiGreenMergeRequest(
            "42", "attempt-1", "agent-smith/42", [new AgentSmith.Contracts.Runs.CiGreenPullRequest("primary", "https://stub.test/primary/pull/1")])
        { AskedBy = "Dana" };
        runner.RequestContext[AgentSmith.Contracts.Commands.ContextKeys.ActOutcome] = slot;

        var result = await runner.RunAsync(AgentSmith.Contracts.Commands.PipelinePresets.MergeGreenName);

        result.IsSuccess.Should().BeTrue(result.Message);
        merges.Should().Equal(BranchProtection.Respect);
        slot.Value.Should().ContainSingle().Which.Outcome.Should().Be(PullRequestCompletionOutcome.Merged);
    }

    private static async Task<AgentSmith.Domain.Models.CommandResult> RunGreenAsync(List<BranchProtection> merges, ReviewMode review)
    {
        await using var harness = RealCompositionHarness.Build(FixturePaths.For(FixturePaths.Default), services =>
        {
            HarnessProjectAnalyzerStub.Register(services);
            services.RemoveAll<ISourceProviderFactory>();
            services.AddSingleton<ISourceProviderFactory>(new RecordingFactory(new StubSourceProviderFactory(), merges));
        });
        harness.ChatClient
            .EnqueueToolCall("write_file", """{"path":"primary/src/Feature.cs","content":"public class Feature {}"}""")
            .EnqueueToolCall("run_command", """{"command":"dotnet test","repo":"primary"}""")
            .EnqueueToolCall("update_progress", """{"items":[{"id":"guard","activity":"Answer an empty request body with 400","status":"done"}]}""")
            .EnqueueReport();
        var runner = new PipelineRunner(harness.Services) { Review = review };
        return await runner.RunAsync("code");
    }

    private sealed class RecordingFactory(ISourceProviderFactory inner, List<BranchProtection> merges) : ISourceProviderFactory
    {
        public ISourceProvider Create(RepoConnection config)
        {
            var proxy = DispatchProxy.Create<ISourceProvider, MergeRecorder>();
            ((MergeRecorder)(object)proxy).Init(inner.Create(config), merges);
            return proxy;
        }
    }

    /// <summary>Forwards every call to the stub and records the protection mode of each merge.</summary>
    public class MergeRecorder : DispatchProxy
    {
        private ISourceProvider _inner = null!;
        private List<BranchProtection> _merges = null!;

        internal void Init(ISourceProvider inner, List<BranchProtection> merges) => (_inner, _merges) = (inner, merges);

        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method!.Name == nameof(ISourceProvider.MergePullRequestAsync)) _merges.Add((BranchProtection)args![2]!);
            return method.Invoke(_inner, args);
        }
    }
}
