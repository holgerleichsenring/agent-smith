using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Specs;
using AgentSmith.PipelineHarness.Composition;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// 2026-11-04-6768a fast-tier end-to-end: a two-spec series where spec 1's checkpoint made spec 2's
/// premise untrue. Spec 2's check reports it false through a search it took, the replay reads the branch
/// — absent at the series base, present at HEAD, touched by a series checkpoint — and spec 2's master
/// runs instead of the spec being handed back.
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class SeriesPremiseSupersessionTests
{
    private const string GreenVerdict =
        """Done. {"status":"green","build_ran":true,"build_passed":true,"tests_ran":true,"tests_passed":true,"summary":"fixed","acceptance":[{"criterion":"criterion 1","status":"met","evidence":"handled"},{"criterion":"criterion 2","status":"met","evidence":"preserved"}]}""";

    private const string TwoSpecs = """
        {"specs": [
           {"slug": "introduce-the-guard",
            "goal": "Introduce the empty-payload guard",
            "steps": [{"id": "guard", "action": "Answer an empty request body with 400"}],
            "done": ["An empty request body is answered with 400."],
            "facts": [{"claim": "no request path validates an empty body today", "cites": "L1"}],
            "carries": [1,2,3]},
           {"slug": "migrate-the-callers",
            "goal": "Move the existing callers onto the guard",
            "steps": [{"id": "callers", "action": "Route every caller through the guard"}],
            "done": ["No caller builds its own empty-payload check."],
            "facts": [{"claim": "no caller references EmptyPayload yet", "cites": "L2"}],
            "carries": [4,5,6]}],
         "discarded": [],
         "ignored_instructions": [],
         "handback": {"case": "none", "reason": ""}}
        """;

    [Fact]
    public async Task SeriesHarness_SiblingMadeAPremiseUntrue_NextSpecReachesItsMaster()
    {
        var tickets = new RecordingTicketProvider();
        await using var harness = RealCompositionHarness.Build(FixturePaths.For(FixturePaths.Default), services =>
        {
            HarnessProjectAnalyzerStub.Register(services);
            services.RemoveAll<ITicketProviderFactory>();
            services.AddSingleton<ITicketProviderFactory>(new RecordingTicketProviderFactory(tickets));
        });
        ScriptTheBranch(harness.StubSandboxFactory!.GitAnswers);
        harness.Services.GetRequiredService<HarnessSpecAccountant>()
            .LeaveOutstanding("No caller builds its own empty-payload check.");
        harness.Services.GetRequiredService<HarnessSpecPremiseChecker>()
            .FindsBySearchOnCall(2, "no caller references EmptyPayload yet", "EmptyPayload", "src");
        harness.ChatClient
            .EnqueueText(TwoSpecs)
            .EnqueueText("Planning: introduce the guard.")
            .EnqueueToolCall("write_file", """{"path":"primary/src/Guard.cs","content":"// guard"}""")
            .EnqueueText(GreenVerdict)
            .EnqueueText("Planning: migrate the callers.")
            .EnqueueToolCall("write_file", """{"path":"primary/src/Callers.cs","content":"// callers"}""")
            .EnqueueText(GreenVerdict);

        var runner = new PipelineRunner(harness.Services) { DoneStatus = "done", FailedStatus = "failed" };
        var result = await runner.RunAsync("code");

        result.IsSuccess.Should().BeTrue();
        harness.Services.GetRequiredService<HarnessSpecPremiseChecker>().Asked.Should().HaveCount(2);
        var progress = runner.LastContext!.Get<SpecSequenceProgress>(ContextKeys.SpecSequenceProgress);
        progress.Specs.Should().HaveCount(2).And.OnlyContain(p => p.State == SpecRunState.Done,
            "spec 2's premise was made untrue by spec 1's own checkpoint, so spec 2 was built");
        harness.ChatClient.PromptsSeen.Should().Contain(p => p.Contains("Move the existing callers onto the guard"),
            "spec 2's master was asked to build it");
        tickets.Commented.Select(c => c.Comment).Should().NotContain(c => c.Contains(
            Application.Services.Specs.PremiseHandback.Heading));
    }

    // The branch as git would answer it: one revision of the series, two checkpoints after it, the cited
    // search empty at the base and matching at HEAD, and the later checkpoint touching the matched path.
    private static void ScriptTheBranch(Dictionary<string, (int ExitCode, string Output)> git)
    {
        git["symbolic-ref --short refs/remotes/origin/HEAD"] = (0, "origin/main\n");
        git["log --format=%H%x09%s --fixed-strings"] = (0, "anchor0001\t{grep} 1 (initial derivation), 2 spec(s)\n");
        git["rev-list --no-merges --fixed-strings"] = (0, "c2c2c2c2c2\nc1c1c1c1c1\n");
        git["grep -InE -e EmptyPayload c1c1c1c1c1^ "] = (1, string.Empty);
        git["grep -InE -e EmptyPayload HEAD "] = (0, "HEAD:src/Callers.cs:4:EmptyPayload\n");
        git["log --no-merges --fixed-strings --grep=[checkpoint]"] = (0, "c2c2c2c2c2\n");
    }
}
