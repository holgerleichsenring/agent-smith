using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.PipelineHarness.Composition;
using FluentAssertions;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// p0331 fast-tier end-to-end on a TWO-repo project. 2026-11-18-9c1cb: the scope call names repositories but
/// narrows nothing — both are provisioned and the master edits the one it means.
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
    public async Task Code_ClassifierNamesOneRepo_KeepsBoth()
    {
        await using var harness = RealCompositionHarness.Build(
            FixturePaths.For(FixturePaths.Default), HarnessProjectAnalyzerStub.Register);
        harness.ChatClient
            // ScopeRepos: the classifier names one of the two repos; 2026-11-18-9c1cb: the run keeps both.
            .EnqueueScopeReply("""{"repos":[{"name":"primary","affected":true,"confidence":0.95},{"name":"secondary","affected":false,"confidence":0.9,"reason":"not named by the ticket"}],"rationale":"The ticket names the primary service only."}""")
            // p0390: DeriveSpecification runs before the master and drains one FIFO slot.
            // p0394a: no plan slot — the spec is the plan, the master consumes next.
            // Two repositories in scope, so the spec names the one it changes.
            .EnqueueText(SpecDerivationFixture.DerivationJson.Replace("\"carries\"", "\"repos\": [\"primary\"], \"carries\"", StringComparison.Ordinal))
            .EnqueueToolCall("write_file", """{"path":"primary/src/Patch.cs","content":"// scoped fix"}""")
            .EnqueueToolCall("run_command", """{"command":"dotnet build","repo":"primary"}""")
            .EnqueueToolCall("update_progress", """{"items":[{"id":"guard","activity":"Answer an empty request body with 400","status":"done"}]}""")
            .EnqueueFinish(summary: "fixed");

        var runner = new PipelineRunner(harness.Services) { ReposOverride = TwoRepos() };
        var result = await runner.RunAsync("code");

        result.IsSuccess.Should().BeTrue($"a real change and a green verdict pass: {result.Message}");

        // Nothing narrowed: both repositories provisioned; the master scopes itself.
        harness.StubSandboxFactory!.Spawned.Should().HaveCount(2, "no model call narrows the run's repositories");

        var pipeline = runner.LastContext!;
        pipeline.Get<IReadOnlyList<RepoConnection>>(ContextKeys.Repos).Select(r => r.Name)
            .Should().Equal(["primary", "secondary"]);

        pipeline.TryGet<List<OpenedPullRequest>>(ContextKeys.OpenedPullRequests, out var prs).Should().BeTrue();
        prs!.Should().Contain(pr => pr.RepoName == "primary");

        // The master's write reached the scoped repo's sandbox as a real step.
        harness.StubSandboxFactory.Spawned
            .SelectMany(s => s.Sandbox.RanSteps)
            .Should().Contain(s => s.Kind == AgentSmith.Sandbox.Wire.StepKind.WriteFile
                && s.Path != null && s.Path.EndsWith("src/Patch.cs", StringComparison.Ordinal));
    }

    [Fact]
    public async Task FixBug_ClassifierGarbage_KeepsAllRepos()
    {
        // An unusable classification leaves both repos provisioned.
        await using var harness = RealCompositionHarness.Build(
            FixturePaths.For(FixturePaths.Default), HarnessProjectAnalyzerStub.Register);
        harness.ChatClient
            .EnqueueScopeReply("I think it is probably the primary one?") // no JSON — parse failure
            // p0390: DeriveSpecification runs before the master and drains one FIFO slot.
            // p0394a: no plan slot — the spec is the plan, the master consumes next.
            .EnqueueText(SpecDerivationFixture.DerivationJson)
            .EnqueueText("No changes needed.");

        var runner = new PipelineRunner(harness.Services) { ReposOverride = TwoRepos() };
        var result = await runner.RunAsync("code");

        result.Should().NotBeNull("the pipeline must run to a terminal result");
        harness.StubSandboxFactory!.Spawned.Should().HaveCount(2,
            "a failed classification keeps the full scope (all repos provisioned)");
        var pipeline = runner.LastContext!;
        pipeline.Get<IReadOnlyList<RepoConnection>>(ContextKeys.Repos).Should().HaveCount(2);
    }
}
