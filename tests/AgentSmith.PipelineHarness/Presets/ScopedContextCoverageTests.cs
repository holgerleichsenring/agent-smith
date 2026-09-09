using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.PipelineHarness.Composition;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// 2026-09-08-1830 fast-tier end-to-end: run 8688's shape. One repository with two
/// contexts; the scope call names both; the first derivation cuts frontend only. Live,
/// that run went green with backend untouched. Now the cut is pinned back once — a
/// second cut carrying both contexts proceeds as two phases — and a gap that survives
/// the pin parks the ticket with a question naming it.
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class ScopedContextCoverageTests
{
    private const string ScopeReply =
        """{"repos":[{"name":"primary","affected":true,"confidence":0.95}],"contexts":{"primary":["frontend","backend"]},"rationale":"both the frontend and backend contexts need dependency auditing"}""";

    private const string FrontendOnlyJson = """
        {"phases": [
           {"slug": "frontend-dependency-updates",
            "goal": "Raise the frontend package floors the audit names",
            "contexts": ["frontend"],
            "steps": [{"id": "raise", "action": "Raise the versions in frontend/"}],
            "done": ["frontend/package.json carries versions the audit no longer flags."],
            "carries": [1,2,3,4,5,6,7,8,9,10,11,12]}],
         "discarded": [],
         "discarded_contexts": [],
         "ignored_instructions": [],
         "handback": {"case": "none", "reason": ""}}
        """;

    private const string BothContextsJson = """
        {"phases": [
           {"slug": "frontend-dependency-updates",
            "goal": "Raise the frontend package floors the audit names",
            "contexts": ["frontend"],
            "steps": [{"id": "raise", "action": "Raise the versions in frontend/"}],
            "done": ["frontend/package.json carries versions the audit no longer flags."],
            "carries": [1,2,3,4,5,6]},
           {"slug": "backend-dependency-updates",
            "goal": "Raise the backend package floors the audit names",
            "contexts": ["backend"],
            "steps": [{"id": "raise", "action": "Raise the versions in backend/"}],
            "done": ["backend/package.json carries versions the audit no longer flags."],
            "carries": [7,8,9,10,11,12]}],
         "discarded": [],
         "discarded_contexts": [],
         "ignored_instructions": [],
         "handback": {"case": "none", "reason": ""}}
        """;

    private const string GreenVerdict =
        """Done. {"status":"green","build_ran":true,"build_passed":true,"tests_ran":true,"tests_passed":true,"summary":"raised","acceptance":[{"criterion":"criterion 1","status":"met","evidence":"raised"},{"criterion":"criterion 2","status":"met","evidence":"preserved"}]}""";

    [Fact]
    public async Task Harness_ScopeNamesTwoContextsAndTheCutCarriesOne_IsRederivedToBoth()
    {
        var tickets = new RecordingTicketProvider();
        await using var harness = BuildHarness(tickets);
        harness.Services.GetRequiredService<HarnessSpecAccountant>()
            .LeaveOutstanding("backend/package.json carries versions the audit no longer flags.");
        harness.ChatClient
            .EnqueueScopeReply(ScopeReply)
            .EnqueueText(FrontendOnlyJson)
            .EnqueueText(BothContextsJson)
            .EnqueueText("Planning: raise the frontend floors.")
            .EnqueueToolCall("write_file", """{"path":"primary/frontend/package.json","content":"{}"}""")
            .EnqueueText(GreenVerdict)
            .EnqueueText("Planning: raise the backend floors.")
            .EnqueueToolCall("write_file", """{"path":"primary/backend/package.json","content":"{}"}""")
            .EnqueueText(GreenVerdict);

        var runner = new PipelineRunner(harness.Services) { NeedsClarificationStatus = "needs-info" };
        var result = await runner.RunAsync("code");

        result.IsSuccess.Should().BeTrue(result.Message);
        var pipeline = runner.LastContext!;
        pipeline.Get<ScopeNamedContexts>(ContextKeys.ScopeNamedContexts).Contexts.Should().Equal("frontend", "backend");
        var set = pipeline.Get<SpecSet>(ContextKeys.SpecSet);
        set.Phases.Should().HaveCount(2, "the frontend-only cut was pinned back and re-derived");
        set.Phases.SelectMany(p => p.Draft.Contexts).Should().BeEquivalentTo(["frontend", "backend"]);
        pipeline.Get<SpecSequenceProgress>(ContextKeys.SpecSequenceProgress).Phases
            .Should().OnlyContain(p => p.State == PhaseRunState.Done);
        harness.ChatClient.ToolCalls.ShouldHaveCalledInOrder("write_file", "write_file");
        tickets.Finalized.Should().NotContain(f => f.Status == "needs-info");
        tickets.Commented.Should().Contain(c => c.Comment.Contains("**Contexts:** backend"),
            "the author sees which context each phase changes");
    }

    [Fact]
    public async Task Harness_ScopeNamesTwoContextsAndTheGapPersists_ParksWithTheQuestion()
    {
        var tickets = new RecordingTicketProvider();
        await using var harness = BuildHarness(tickets);
        harness.ChatClient
            .EnqueueScopeReply(ScopeReply)
            .EnqueueText(FrontendOnlyJson)
            .EnqueueText(FrontendOnlyJson);

        var runner = new PipelineRunner(harness.Services) { NeedsClarificationStatus = "needs-info" };
        var result = await runner.RunAsync("code");

        result.IsSuccess.Should().BeTrue("a question parks the run; it is not a failed step");
        result.Message.Should().Contain("awaiting_user_input");
        runner.LastContext!.Get<SpecHandback>(ContextKeys.SpecHandback).Case.Should().Be(SpecHandbackCase.Question);
        harness.ChatClient.ToolCalls.Should().BeEmpty("nothing is built on a cut that drops a named context");
        var park = tickets.Finalized.Should().ContainSingle().Subject;
        park.Status.Should().Be("needs-info");
        park.Comment.Should().Contain("(a) carry backend as well")
            .And.Contain("(b) backend is out of scope for this ticket")
            .And.Contain("both the frontend and backend contexts need dependency auditing");
    }

    private static RealCompositionHarness BuildHarness(RecordingTicketProvider tickets) =>
        RealCompositionHarness.Build(FixturePaths.For(FixturePaths.Default), services =>
        {
            HarnessProjectAnalyzerStub.Register(services);
            services.RemoveAll<ITicketProviderFactory>();
            services.AddSingleton<ITicketProviderFactory>(new RecordingTicketProviderFactory(tickets));
            services.RemoveAll<ISourceProviderFactory>();
            services.AddSingleton<ISourceProviderFactory>(new MultiContextSourceProviderFactory(["frontend", "backend"]));
        });
}
