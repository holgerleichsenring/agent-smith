using AgentSmith.Contracts.Commands;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Specs;
using AgentSmith.PipelineHarness.Composition;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// 2026-10-12-3a36e through the REAL composition: a code run over two repositories derives
/// specs that name the repositories they change. 2026-10-12-f5f7a: exactly one each — work in
/// both is cut as two specs, and a reply that names none (a catalog released before the field
/// existed) is asked again and, still naming none, hands the ticket back rather than guessing.
/// 2026-10-12-d142: as its own case, with its own comment, parked for clarification.
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class SpecReposTests
{
    private const string ServerOnlyDerivation = """
        {"specs": [
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

    private const string BothReposDerivation = """
        {"specs": [
           {"slug": "retry-policy-in-worker",
            "goal": "Expose the retry policy from the worker",
            "repos": "BackgroundWorker",
            "steps": [{"id": "expose", "action": "Expose the retry policy from the worker"}],
            "done": ["The worker exposes its retry policy."],
            "carries": [1,2,3,4,5,6]},
           {"slug": "retry-policy-in-server",
            "goal": "Read the worker's retry policy in the server",
            "repos": ["Server"],
            "steps": [{"id": "read", "action": "Read the retry policy in the server"}],
            "done": ["The server applies the worker's retry policy."],
            "carries": [7,8,9,10,11,12]}],
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

        set.Specs.Should().ContainSingle().Which.Draft.Repos.Should().Equal("Server");
        set.Specs[0].Draft.Yaml.Should().Contain("repos:");
    }

    [Fact]
    public async Task Harness_ChangeInBothRepos_DerivesTwoSpecsOneRepositoryEach()
    {
        var set = await DeriveAsync(BothReposDerivation);

        set.Specs.Should().HaveCount(2);
        set.Specs[0].Draft.Repos.Should().Equal("BackgroundWorker");
        set.Specs[1].Draft.Repos.Should().Equal("Server");
        set.Specs[1].Draft.Requires.Should().Equal([set.Specs[0].SpecId], "the dependent spec comes second");
    }

    [Fact]
    public async Task Harness_ReplyWithoutReposOverTwoRepos_HandsBack()
    {
        var tickets = new RecordingTicketProvider();
        var context = await RunAsync(SpecDerivationFixture.DerivationJson, tickets);

        var handback = context.Get<SpecHandback>(ContextKeys.SpecHandback);
        handback.Case.Should().Be(SpecHandbackCase.ScopeUnassigned);
        handback.Reason.Should().Contain("BackgroundWorker, Server",
            "a cut that cannot name one repository is handed back, not copied whole into a guessed one");
        var park = tickets.Finalized.Should().ContainSingle().Subject;
        park.Status.Should().Be("needs-info");
        park.Comment.Should().Contain(SpecHandbackComment.ScopeUnassignedMarker)
            .And.NotContain(SpecHandbackComment.ContradictionMarker);
    }

    private static async Task<SpecSet> DeriveAsync(string derivation) =>
        (await RunAsync(derivation)).Get<SpecSet>(ContextKeys.SpecSet);

    private static async Task<PipelineContext> RunAsync(string derivation, RecordingTicketProvider? tickets = null)
    {
        await using var harness = RealCompositionHarness.Build(FixturePaths.For(FixturePaths.Default), services =>
        {
            HarnessProjectAnalyzerStub.Register(services);
            if (tickets is null) return;
            services.RemoveAll<ITicketProviderFactory>();
            services.AddSingleton<ITicketProviderFactory>(new RecordingTicketProviderFactory(tickets));
        });
        harness.ChatClient
            .EnqueueScopeReply("no verdict") // unusable classification keeps both repositories
            .EnqueueText(derivation)
            .EnqueueText("No changes needed.");

        var runner = new PipelineRunner(harness.Services) { ReposOverride = TwoRepos(), NeedsClarificationStatus = "needs-info" };
        await runner.RunAsync("code");
        return runner.LastContext!;
    }
}
