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

    private static async Task<AgentSmith.Domain.Models.CommandResult> RunGreenAsync(List<BranchProtection> merges, MergePolicyConfig? merge)
    {
        await using var harness = RealCompositionHarness.Build(FixturePaths.For(FixturePaths.Default), services =>
        {
            HarnessProjectAnalyzerStub.Register(services);
            services.RemoveAll<ISourceProviderFactory>();
            services.AddSingleton<ISourceProviderFactory>(new RecordingFactory(new StubSourceProviderFactory(), merges));
        });
        harness.ChatClient
            .EnqueueText(SpecDerivationFixture.DerivationJson)
            .EnqueueToolCall("write_file", """{"path":"primary/src/Feature.cs","content":"public class Feature {}"}""")
            .EnqueueToolCall("run_command", """{"command":"dotnet test","repo":"primary"}""")
            .EnqueueToolCall("update_progress", """{"items":[{"id":"guard","activity":"Answer an empty request body with 400","status":"done"}]}""")
            .EnqueueText("""Feature added; tests green. {"status":"green","build_ran":true,"build_passed":true,"tests_ran":true,"tests_passed":true,"summary":"feature implemented","acceptance":[{"criterion":"criterion 1","status":"met","evidence":"handled in the change"},{"criterion":"criterion 2","status":"met","evidence":"existing behaviour preserved"}]}""");
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
