using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Models;
using AgentSmith.PipelineHarness.Composition;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// 2026-09-17-0e79c fast-tier end-to-end: the premise check really runs inside the phase block,
/// once per phase, before that phase's master — and a false premise stops its phase while the
/// phases that were already verified are still delivered.
/// <para>
/// A unit test over the handler proves what the step decides; only a preset run proves that the
/// step is reached at all, in the right place, with the right things in its hands.
/// </para>
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class PremiseCheckPhaseBlockTests
{
    private const string GreenVerdict =
        """Done. {"status":"green","build_ran":true,"build_passed":true,"tests_ran":true,"tests_passed":true,"summary":"fixed","acceptance":[{"criterion":"criterion 1","status":"met","evidence":"handled"},{"criterion":"criterion 2","status":"met","evidence":"preserved"}]}""";

    /// <summary>Two phases that each STATE something. A cite nothing minted is downgraded to an
    /// assumption, which is still a premise — so the check has something to be asked about.</summary>
    private const string TwoPhasesStatingPremises = """
        {"phases": [
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
            "facts": [{"claim": "three callers build their own empty-payload check", "cites": "L2"}],
            "carries": [4,5,6]}],
         "discarded": [],
         "ignored_instructions": [],
         "handback": {"case": "none", "reason": ""}}
        """;

    [Fact]
    public async Task Harness_EachDerivedPhase_IsAskedAboutItsOwnPremisesBeforeItsMaster()
    {
        var tickets = new RecordingTicketProvider();
        await using var harness = BuildHarness(tickets);
        harness.Services.GetRequiredService<HarnessSpecAccountant>()
            .LeaveOutstanding("No caller builds its own empty-payload check.");
        harness.ChatClient
            .EnqueueText(TwoPhasesStatingPremises)
            .EnqueueText("Planning: introduce the guard.")
            .EnqueueToolCall("write_file", """{"path":"primary/src/Guard.cs","content":"// guard"}""")
            .EnqueueText(GreenVerdict)
            .EnqueueText("Planning: migrate the callers.")
            .EnqueueToolCall("write_file", """{"path":"primary/src/Callers.cs","content":"// callers"}""")
            .EnqueueText(GreenVerdict);

        var runner = new PipelineRunner(harness.Services) { DoneStatus = "done", FailedStatus = "failed" };
        var result = await runner.RunAsync("code");

        result.IsSuccess.Should().BeTrue();
        var asked = harness.Services.GetRequiredService<HarnessPhasePremiseChecker>().Asked;
        asked.Should().HaveCount(2, "one call per phase, and no second pass");
        asked[0].Claims.Should().Contain(c => c.Contains("no request path validates an empty body"));
        asked[0].AlreadyRan.Should().BeEmpty("the first phase of a set has no predecessor");
        asked[1].Claims.Should().Contain(c => c.Contains("three callers build their own"));
        asked[1].AlreadyRan.Should().Equal([asked[0].PhaseId],
            "phase 1's work is committed in the sandbox phase 2's premises are checked against");
        asked[1].Repositories.Should().NotBeEmpty("a look it can address, or every call is refused");
    }

    [Fact]
    public async Task Harness_FalsePremiseOnTheSecondPhase_StopsItAndTheFirstIsStillDelivered()
    {
        var tickets = new RecordingTicketProvider();
        await using var harness = BuildHarness(tickets);
        harness.Services.GetRequiredService<HarnessSpecAccountant>()
            .LeaveOutstanding("No caller builds its own empty-payload check.");
        harness.Services.GetRequiredService<HarnessPhasePremiseChecker>()
            .FindsOnCall(2, new PremiseFinding(
                "three callers build their own empty-payload check",
                "no-longer-holds", "only one caller does, and it already uses the guard",
                "M1", "[M1] primary: the premise check ran 'grep -rn EmptyPayload' exited 1",
                "primary: grep -rn EmptyPayload"));
        harness.ChatClient
            .EnqueueText(TwoPhasesStatingPremises)
            .EnqueueText("Planning: introduce the guard.")
            .EnqueueToolCall("write_file", """{"path":"primary/src/Guard.cs","content":"// guard"}""")
            .EnqueueText(GreenVerdict);

        var runner = new PipelineRunner(harness.Services) { DoneStatus = "done", FailedStatus = "failed" };
        var result = await runner.RunAsync("code");

        result.IsSuccess.Should().BeTrue("the phase that verified reached its pull request");
        var shortfall = Contracts.Specs.RunShortfall.DeliveredOn(runner.LastContext!);
        shortfall.Should().NotBeNull();
        shortfall!.Delivered.Should().ContainSingle("phase 1 was built, verified and recorded");
        shortfall.NotDelivered.Should().ContainSingle("phase 2 never spent a master token");

        var stopped = runner.LastContext!
            .Get<Contracts.Specs.SpecSequenceProgress>(ContextKeys.SpecSequenceProgress)
            .Phases.Single(p => p.PhaseId == shortfall.NotDelivered[0].PhaseId);
        stopped.State.Should().Be(Contracts.Specs.PhaseRunState.HandedBack,
            "never built, rather than built and found red");
        stopped.FailingCommand.Should().Contain("False premise")
            .And.Contain("three callers build their own empty-payload check")
            .And.Contain("grep -rn EmptyPayload", "what it looked at")
            .And.Contain("[M1]", "and the framework's own line for that look");
        tickets.Commented.Select(c => c.Comment).Should().Contain(c => c.Contains(
            Application.Services.Specs.PremiseHandback.Heading),
            "the person holding the ticket is told which premise, and where a change is made");
    }

    private static RealCompositionHarness BuildHarness(RecordingTicketProvider tickets) =>
        RealCompositionHarness.Build(FixturePaths.For(FixturePaths.Default), services =>
        {
            HarnessProjectAnalyzerStub.Register(services);
            services.RemoveAll<ITicketProviderFactory>();
            services.AddSingleton<ITicketProviderFactory>(new RecordingTicketProviderFactory(tickets));
        });
}
