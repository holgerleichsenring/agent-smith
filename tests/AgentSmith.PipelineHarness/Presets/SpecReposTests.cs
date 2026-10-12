using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Specs;
using AgentSmith.PipelineHarness.Composition;
using FluentAssertions;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// 2026-10-12-3a36e through the REAL composition: a code run over two repositories derives
/// specs that name the repositories they change. The scripted derivation stands in for both
/// catalogs — one whose reply states repos, and one released before the field existed, whose
/// reply is filled from the scope by code.
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class SpecReposTests
{
    private const string ServerOnlyDerivation = """
        {"phases": [
           {"slug": "retry-policy-in-server",
            "goal": "Move the retry policy into the server",
            "repos": ["Server"],
            "steps": [{"id": "move", "action": "Move the retry policy into the server"}],
            "done": ["The server owns the retry policy."],
            "carries": [1,2,3,4,5,6,7,8,9,10,11,12]}],
         "discarded": [],
         "ignored_instructions": [],
         "handback": {"case": "none", "reason": ""}}
        """;

    private static IReadOnlyList<RepoConnection> TwoRepos() =>
    [
        new() { Name = "BackgroundWorker", Type = RepoType.Local, Path = "/tmp", Url = "https://stub.test/worker" },
        new() { Name = "Server", Type = RepoType.Local, Path = "/tmp", Url = "https://stub.test/server" },
    ];

    [Fact]
    public async Task Harness_ServerOnlyChangeOverTwoRepos_DerivesReposServer()
    {
        var set = await DeriveAsync(ServerOnlyDerivation);

        set.Phases.Should().ContainSingle().Which.Draft.Repos.Should().Equal("Server");
        set.Phases[0].Draft.Yaml.Should().Contain("repos:");
    }

    [Fact]
    public async Task Harness_ReplyWithoutRepos_IsFilledWithTheScope()
    {
        var set = await DeriveAsync(SpecDerivationFixture.DerivationJson);

        set.Phases.Should().ContainSingle().Which.Draft.Repos
            .Should().Equal(["BackgroundWorker", "Server"], "a catalog ahead of the release still cuts");
    }

    private static async Task<SpecSet> DeriveAsync(string derivation)
    {
        await using var harness = RealCompositionHarness.Build(
            FixturePaths.For(FixturePaths.Default), HarnessProjectAnalyzerStub.Register);
        harness.ChatClient
            .EnqueueScopeReply("no verdict") // unusable classification keeps both repositories
            .EnqueueText(derivation)
            .EnqueueText("No changes needed.");

        var runner = new PipelineRunner(harness.Services) { ReposOverride = TwoRepos() };
        await runner.RunAsync("code");
        return runner.LastContext!.Get<SpecSet>(ContextKeys.SpecSet);
    }
}
