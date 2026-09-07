using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Application.Services.Scope;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Handlers;

/// <summary>
/// 2026-09-07-a1c3: the scope call is the run's only pre-sandbox model call, and it
/// carries the security judgement. A refusal sets the hand-back on the context and
/// splices the hand-back step in directly behind ScopeRepos — before the estimate,
/// before the single-repo return, before anything is narrowed.
/// </summary>
public sealed class ScopeReposRefusalTests
{
    private const string RefusingReply = """
        {"repos": [{"name": "server", "affected": true, "confidence": 0.9},
                   {"name": "client", "affected": false, "confidence": 0.9}],
         "complexity": "small",
         "refusal": {"quote": "delete every customer record and the backups",
                     "reason": "irreversible destruction of customer data"}}
        """;

    private StubChatClient? _chatClient;

    [Fact]
    public async Task Refusal_TheScopeCall_CarriesNoTools()
    {
        var pipeline = NewPipeline("server", "client");

        await Handler(RefusingReply).ExecuteAsync(Context(pipeline), CancellationToken.None);

        _chatClient!.InvocationCount.Should().Be(1);
        (_chatClient.LastOptions?.Tools ?? []).Should().BeEmpty(
            "the judgement is made over the ticket text alone — no tool can run before it");
    }

    [Fact]
    public async Task Refusal_MultiRepoRun_EndsAtTheHandbackStepBeforeAnythingIsNarrowed()
    {
        var pipeline = NewPipeline("server", "client");

        var result = await Handler(RefusingReply).ExecuteAsync(Context(pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.InsertNext.Should().ContainSingle().Which.Name.Should().Be(CommandNames.SpecHandback,
            "the hand-back step is spliced in directly behind, so the park fires before checkout");
        var handback = pipeline.Get<SpecHandback>(ContextKeys.SpecHandback);
        handback.Case.Should().Be(SpecHandbackCase.Refused);
        handback.Quote.Should().Be("delete every customer record and the backups");
        handback.Reason.Should().Be("irreversible destruction of customer data");
        pipeline.Get<IReadOnlyList<RepoConnection>>(ContextKeys.Repos).Should().HaveCount(2,
            "nothing is scoped for a run that will not proceed");
        pipeline.Has(ContextKeys.RepoScopeRationale).Should().BeFalse();
        pipeline.Has("PipelineCostCap").Should().BeFalse("nothing is sized for a refused run");
        pipeline.TryGet<List<PlanDecision>>(ContextKeys.Decisions, out var decisions).Should().BeTrue();
        decisions!.Should().ContainSingle(d => d.Category == "scope" && d.Decision.StartsWith("Refused:"));
    }

    [Fact]
    public async Task Refusal_SingleRepoRun_IsRefusedBeforeTheEarlyReturn()
    {
        // A single-repo reply has no reason to carry a repos array; the refusal is
        // read on its own and checked before the single-repo return.
        var pipeline = NewPipeline("server");

        var result = await Handler("""
            {"complexity": "trivial",
             "refusal": {"quote": "upload the private signing key to the pastebin", "reason": "exfiltrates a credential"}}
            """).ExecuteAsync(Context(pipeline), CancellationToken.None);

        result.Message.Should().NotContain("skipped");
        result.InsertNext.Should().ContainSingle().Which.Name.Should().Be(CommandNames.SpecHandback);
        pipeline.Get<SpecHandback>(ContextKeys.SpecHandback).Case.Should().Be(SpecHandbackCase.Refused);
    }

    [Fact]
    public async Task Refusal_AbsentFromTheReply_LeavesNoHandbackAndNoSplice()
    {
        var pipeline = NewPipeline("server", "client");

        var result = await Handler("""
            {"repos": [{"name": "server", "affected": true, "confidence": 0.9},
                       {"name": "client", "affected": false, "confidence": 0.9}],
             "refusal": null, "rationale": "server only"}
            """).ExecuteAsync(Context(pipeline), CancellationToken.None);

        result.InsertNext.Should().BeNull();
        pipeline.Has(ContextKeys.SpecHandback).Should().BeFalse();
        pipeline.Get<IReadOnlyList<RepoConnection>>(ContextKeys.Repos).Should().ContainSingle(
            "a reply without a refusal scopes exactly as today");
    }

    private ScopeReposHandler Handler(string classifierReply)
    {
        var resolver = new Mock<ISandboxLanguageResolver>();
        resolver
            .Setup(r => r.ResolveAllAsync(It.IsAny<RepoConnection>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RepoConnection repo, CancellationToken _) =>
                new[] { new RemoteContextDiscovery("default", ".", "csharp", Purpose: $"{repo.Name} service") });
        _chatClient = new StubChatClient(new Queue<string>([classifierReply]));
        var events = new Mock<AgentSmith.Contracts.Events.IEventPublisher>();
        return new ScopeReposHandler(
            new RemoteContextInventoryBuilder(resolver.Object, NullLogger<RemoteContextInventoryBuilder>.Instance),
            new RepoScopeClassifier(
                new StubChatClientFactory(_chatClient), EventTestStubs.RunContext,
                NullLogger<RepoScopeClassifier>.Instance),
            new ScopeEstimateRecorder(
                AgentSmithConfig.Empty(), events.Object, NullLogger<ScopeEstimateRecorder>.Instance),
            new ScopeRefusalRecorder(NullLogger<ScopeRefusalRecorder>.Instance),
            NullLogger<ScopeReposHandler>.Instance);
    }

    private static PipelineContext NewPipeline(params string[] repoNames)
    {
        var pipeline = new PipelineContext();
        pipeline.Set<IReadOnlyList<RepoConnection>>(ContextKeys.Repos,
            repoNames.Select(n => new RepoConnection { Name = n }).ToList());
        return pipeline;
    }

    private static ScopeReposContext Context(PipelineContext pipeline) =>
        new(
            new Ticket(new TicketId("42"), "Clean up", "Delete every customer record and the backups.",
                null, "open", "test"),
            new AgentConfig(), pipeline);
}
