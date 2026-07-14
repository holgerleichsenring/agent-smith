using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.PipelineHarness.Composition;
using FluentAssertions;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// p0331 fast-tier end-to-end: fix-bug on a TWO-repo project where the scripted
/// classifier narrows the run to one repo BEFORE any sandbox exists. Proves the
/// whole ticket-scoped provisioning chain through the real composition:
/// ScopeRepos → narrowed ContextKeys.Repos → ONE sandbox spawned → master edits
/// the scoped repo → CommitAndPR opens a PR ONLY for the scoped repo, with the
/// scope rationale recorded on the run.
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class ScopeReposTests
{
    private static IReadOnlyList<RepoConnection> TwoRepos() =>
    [
        new() { Name = "primary", Type = RepoType.Local, Path = "/tmp", Url = "https://stub.test/primary" },
        new() { Name = "secondary", Type = RepoType.Local, Path = "/tmp", Url = "https://stub.test/secondary" },
    ];

    [Fact]
    public async Task FixBug_NarrowedScope_CommitsAndPrsOnlyTheScopedRepo()
    {
        await using var harness = RealCompositionHarness.Build(
            FixturePaths.For(FixturePaths.Default), HarnessProjectAnalyzerStub.Register);
        harness.ChatClient
            // ScopeRepos: the classifier confidently names ONE of the two repos.
            .EnqueueText("""{"repos":["primary"],"confidence":0.95,"rationale":"The ticket names the primary service only."}""")
            // p0328: NegotiateExpectation drafts before planning and drains one FIFO slot.
            .EnqueueText(ExpectationNegotiationTests.DraftJson)
            // GeneratePlan drains one FIFO slot (p0276).
            .EnqueueText("Planning: I will patch the file.")
            .EnqueueToolCall("write_file", """{"path":"primary/src/Patch.cs","content":"// scoped fix"}""")
            .EnqueueToolCall("run_command", """{"command":"dotnet build","repo":"primary"}""")
            .EnqueueText("""Done. {"status":"green","build_ran":true,"build_passed":true,"tests_ran":true,"tests_passed":true,"summary":"fixed","acceptance":[{"criterion":"criterion 1","status":"met","evidence":"handled in the change"},{"criterion":"criterion 2","status":"met","evidence":"existing behaviour preserved"}]}""");

        var runner = new PipelineRunner(harness.Services) { ReposOverride = TwoRepos() };
        var result = await runner.RunAsync("fix-bug");

        result.IsSuccess.Should().BeTrue(
            $"narrowed scope + real change + green verdict must pass the keystone: {result.Message}");

        // Provisioning was narrowed: ONE sandbox for the whole 2-repo project.
        harness.StubSandboxFactory!.Spawned.Should().HaveCount(1,
            "only the scoped repo may be provisioned — the descoped repo costs nothing");

        var pipeline = runner.LastContext!;
        pipeline.Get<IReadOnlyList<RepoConnection>>(ContextKeys.Repos)
            .Should().ContainSingle().Which.Name.Should().Be("primary");

        // The scope decision is visible on the run, never silent.
        pipeline.TryGet<string>(ContextKeys.RepoScopeRationale, out var rationale).Should().BeTrue();
        rationale.Should().Contain("primary").And.Contain("The ticket names the primary service only.");

        // Commit + PR touched ONLY the scoped repo.
        pipeline.TryGet<List<OpenedPullRequest>>(ContextKeys.OpenedPullRequests, out var prs).Should().BeTrue();
        prs!.Should().OnlyContain(pr => pr.RepoName == "primary",
            "the descoped repo must not get a commit or PR");

        // The master's write reached the scoped repo's sandbox as a real step.
        harness.StubSandboxFactory.Spawned
            .SelectMany(s => s.Sandbox.RanSteps)
            .Should().Contain(s => s.Kind == AgentSmith.Sandbox.Wire.StepKind.WriteFile
                && s.Path != null && s.Path.EndsWith("src/Patch.cs", StringComparison.Ordinal));
    }

    [Fact]
    public async Task FixBug_ClassifierGarbage_KeepsAllRepos()
    {
        // The conservative construction: an unusable classification must leave
        // today's behavior intact — both repos provisioned, fallback recorded.
        await using var harness = RealCompositionHarness.Build(
            FixturePaths.For(FixturePaths.Default), HarnessProjectAnalyzerStub.Register);
        harness.ChatClient
            .EnqueueText("I think it is probably the primary one?") // no JSON — parse failure
            // p0328: NegotiateExpectation drafts before planning and drains one FIFO slot.
            .EnqueueText(ExpectationNegotiationTests.DraftJson)
            .EnqueueText("Planning: nothing to do.")
            .EnqueueText("No changes needed.");

        var runner = new PipelineRunner(harness.Services) { ReposOverride = TwoRepos() };
        var result = await runner.RunAsync("fix-bug");

        result.Should().NotBeNull("the pipeline must run to a terminal result");
        harness.StubSandboxFactory!.Spawned.Should().HaveCount(2,
            "a failed classification keeps the full scope (all repos provisioned)");
        var pipeline = runner.LastContext!;
        pipeline.Get<IReadOnlyList<RepoConnection>>(ContextKeys.Repos).Should().HaveCount(2);
        pipeline.TryGet<string>(ContextKeys.RepoScopeRationale, out var rationale).Should().BeTrue();
        rationale.Should().Contain("fallback");
    }
}
