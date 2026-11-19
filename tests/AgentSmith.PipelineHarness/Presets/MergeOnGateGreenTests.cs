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
/// 2026-11-17-023fa: the code preset through the real composition — a green run on a project with merge: on_gate_green
/// merges its pull request in the project's protection mode as its last step; the same run on a manual project
/// merges nothing.
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class MergeOnGateGreenTests
{
    [Theory]
    [InlineData(false, BranchProtection.Bypass)]
    [InlineData(true, BranchProtection.Respect)]
    public async Task GreenRun_OnGateGreen_MergesItsPullRequest(bool respect, BranchProtection expected)
    {
        var merges = new List<BranchProtection>();
        var result = await RunGreenAsync(merges, new MergePolicyConfig { When = MergeWhen.OnGateGreen, RespectBranchProtection = respect });

        result.IsSuccess.Should().BeTrue(result.Message);
        merges.Should().Equal(expected);
    }

    [Fact]
    public async Task GreenRun_Manual_MergesNothing()
    {
        var merges = new List<BranchProtection>();
        var result = await RunGreenAsync(merges, merge: null);

        result.IsSuccess.Should().BeTrue(result.Message);
        merges.Should().BeEmpty();
    }

    // 2026-11-17-3435b: on_ci_green — the code run merges nothing; the merge-green run merges what CI cleared.
    [Fact]
    public async Task GreenRun_OnCiGreen_MergesNothing()
    {
        var merges = new List<BranchProtection>();
        var result = await RunGreenAsync(merges, new MergePolicyConfig { When = MergeWhen.OnCiGreen });

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
        var runner = new PipelineRunner(harness.Services) { MergeOverride = new MergePolicyConfig { When = MergeWhen.OnCiGreen } };
        runner.RequestContext[AgentSmith.Contracts.Commands.ContextKeys.CiGreenMerge] = new AgentSmith.Contracts.Runs.CiGreenMergeRequest(
            "42", "attempt-1", "agent-smith/42", [new AgentSmith.Contracts.Runs.CiGreenPullRequest("primary", "https://stub.test/primary/pull/1")]);

        var result = await runner.RunAsync(AgentSmith.Contracts.Commands.PipelinePresets.MergeGreenName);

        result.IsSuccess.Should().BeTrue(result.Message);
        merges.Should().Equal(BranchProtection.Bypass);
    }

    private static async Task<AgentSmith.Domain.Models.CommandResult> RunGreenAsync(List<BranchProtection> merges, MergePolicyConfig? merge)
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
            .EnqueueFinish(summary: "feature implemented");
        var runner = new PipelineRunner(harness.Services) { MergeOverride = merge };
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
