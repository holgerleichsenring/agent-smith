using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.PipelineHarness.Composition;
using AgentSmith.Sandbox.Wire;
using FluentAssertions;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// 2026-10-12-f5f7c fast-tier end-to-end: a two-repository code run whose master remembers a
/// fact about the server. The memory is written into the server's commit sandbox, before the
/// server's commit, and the client — which the run did not change — gets neither the memory
/// nor a commit. It used to land in whichever sandbox the map listed first.
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class MemoryPerRepositoryTests
{
    private const string MemoryPath = ".agentsmith/memory/server-build-order.md";

    private static IReadOnlyList<RepoConnection> TwoRepos() =>
    [
        new() { Name = "sample-client", Type = RepoType.Local, Path = "/tmp", Url = "https://stub.test/sample-client" },
        new() { Name = "sample-server", Type = RepoType.Local, Path = "/tmp", Url = "https://stub.test/sample-server" },
    ];

    [Fact]
    public async Task TwoRepos_RememberedServerFact_CommittedOnServersBranchOnly()
    {
        await using var harness = RealCompositionHarness.Build(
            FixturePaths.For(FixturePaths.Default), HarnessProjectAnalyzerStub.Register);
        harness.StubSandboxFactory!.ReportsWorkingTree = true;
        harness.ChatClient
            .EnqueueScopeReply("no verdict") // unusable — the run keeps both repositories
            // 2026-10-12-f5f7a: over two repositories the spec names its one repository.
            .EnqueueText(SpecDerivationFixture.DerivationJson.Replace(
                "\"slug\": \"reject-empty-payloads\",", "\"slug\": \"reject-empty-payloads\", \"repos\": [\"sample-server\"],"))
            .EnqueueToolCall("write_file", """{"path":"sample-server/src/Patch.cs","content":"// fix"}""")
            .EnqueueToolCall("remember", """{"type":"project","name":"server-build-order","description":"Contracts build first","body":"The server builds its contracts first.","repository":"sample-server"}""")
            .EnqueueToolCall("run_command", """{"command":"dotnet build","repo":"sample-server"}""")
            .EnqueueToolCall("update_progress", """{"items":[{"id":"guard","activity":"Answer an empty request body with 400","status":"done"}]}""")
            .EnqueueText("""Done. {"status":"green","build_ran":true,"build_passed":true,"tests_ran":true,"tests_passed":true,"summary":"fixed","acceptance":[{"criterion":"criterion 1","status":"met","evidence":"handled in the change"},{"criterion":"criterion 2","status":"met","evidence":"existing behaviour preserved"}]}""");

        var runner = new PipelineRunner(harness.Services) { ReposOverride = TwoRepos() };
        var result = await runner.RunAsync("code");

        result.IsSuccess.Should().BeTrue($"a real change and a green verdict pass: {result.Message}");
        var sandboxes = harness.StubSandboxFactory.Spawned.Select(s => s.Sandbox).ToList();
        sandboxes.Should().HaveCount(2);
        // The server is the checkout its source change was routed to by name.
        var server = sandboxes.Single(s => s.RanSteps.Any(IsSourceWrite));
        var client = sandboxes.Single(s => s != server);
        var memoryWrite = server.RanSteps.FindIndex(IsMemoryWrite);
        memoryWrite.Should().BeGreaterThan(-1, "the fact is about the server");
        server.RanSteps.FindLastIndex(IsWorkCommit).Should().BeGreaterThan(memoryWrite,
            "the server's commit carries the memory");
        client.RanSteps.Should().NotContain(s => IsMemoryWrite(s), "the client is not what the fact is about");
        client.RanSteps.Should().NotContain(s => IsWorkCommit(s), "the client changed nothing");
        runner.LastContext!.Get<IReadOnlyList<OpenedPullRequest>>(ContextKeys.OpenedPullRequests)
            .Where(o => o.Status == OpenStatus.Opened).Should().ContainSingle()
            .Which.RepoName.Should().Be("sample-server");
    }

    private static bool IsSourceWrite(Step step) =>
        step.Kind == StepKind.WriteFile && step.Path is { } p && p.EndsWith("src/Patch.cs", StringComparison.Ordinal);

    private static bool IsMemoryWrite(Step step) =>
        step.Kind == StepKind.WriteFile && step.Path is { } p && p.EndsWith(MemoryPath, StringComparison.Ordinal);

    private static bool IsWorkCommit(Step step) =>
        step.Command == "git" && step.Args is { } a && a.Contains("commit")
        && !a.Any(x => x.StartsWith("spec: ", StringComparison.Ordinal));
}
