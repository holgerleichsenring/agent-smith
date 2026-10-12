using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Entities;
using AgentSmith.PipelineHarness.Composition;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// 2026-09-07-bd7a fast-tier end-to-end: a requirement that contradicts the repository
/// parks the ticket; re-triggered with nobody having replied and the derivation saying
/// the same thing again, the loop ends as a failed step instead of parking a second
/// time; re-triggered after a reply, it parks again with the reply in view.
/// <para>
/// The second run reads the previous set through a seeded reader and the pointer this
/// system recorded at the first park, the way the question tests do.
/// </para>
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class ContradictionHandbackTests
{
    private const string SpecSha = "spec-sha-1";

    [Fact]
    public async Task Contradiction_TheFirstTime_ParksWhereAPersonCanReply()
    {
        var tickets = new RecordingTicketProvider();
        await using var harness = BuildHarness(tickets);
        harness.ChatClient.EnqueueText(SpecDerivationFixture.ContradictsRepositoryJson);

        var runner = new PipelineRunner(harness.Services) { NeedsClarificationStatus = "needs-info" };
        var result = await runner.RunAsync("code");

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("awaiting_user_input");
        var park = tickets.Finalized.Should().ContainSingle().Subject;
        park.Status.Should().Be("needs-info");
        park.Comment.Should().Contain(Application.Services.Specs.SpecHandbackComment.ContradictionMarker);
    }

    // The loop ends as a FAILED step. Probed before this shape was chosen: a run that
    // CONTINUED past the repeat, as the old branch did, reached CommitAndPR with the spec
    // draft already pushed and finalized the ticket "Completed across 1 repo(s)" with
    // nothing built and the run reported green. (The p0237 finalizer tail used to post that
    // record-PR summary on a failed run before the failure comment landed; 2026-09-07-f420
    // ended that, so the failure is the ticket's only word.)
    [Fact]
    public async Task Contradiction_ASecondTimeWithNoReply_EndsTheLoopAsAFailureNotAPark()
    {
        var tickets = new RecordingTicketProvider([OurContradiction()]);
        await using var harness = BuildHarness(tickets, previous: PreviousContradiction());
        await SeedPointerAsync(harness);
        harness.ChatClient.EnqueueText(SpecDerivationFixture.ContradictsRepositoryJson);

        var runner = new PipelineRunner(harness.Services) { NeedsClarificationStatus = "needs-info" };
        var result = await runner.RunAsync("code");

        result.IsSuccess.Should().BeFalse("nobody replied, so the loop ends — as a failure, never a green run");
        result.Message.Should().Contain("the loop ends here").And.Contain("does not contain");
        tickets.Finalized.Should().NotContain(f => f.Status == "needs-info", "the ticket is not parked a second time");
        tickets.Finalized.Should().ContainSingle("the failure is the ticket's only word")
            .Which.Comment.Should().Contain("Agent Smith — Failed").And.Contain("the loop ends here");
        harness.ChatClient.ToolCalls.Should().BeEmpty("no master runs on a hand-back");
        runner.LastContext!.TryGet<bool>(ContextKeys.OpenQuestionsAwaitingAnswer, out _).Should().BeFalse();
    }

    [Fact]
    public async Task Contradiction_ASecondTimeAfterAReply_ParksAgain()
    {
        var reply = "The client moved to the shared module last sprint — look there.";
        var tickets = new RecordingTicketProvider(
        [
            OurContradiction(),
            new TicketComment("operator", DateTimeOffset.UtcNow.AddHours(-1), reply),
        ]);
        await using var harness = BuildHarness(tickets, previous: PreviousContradiction());
        await SeedPointerAsync(harness);
        harness.ChatClient.EnqueueText(SpecDerivationFixture.ContradictsRepositoryJson);

        var runner = new PipelineRunner(harness.Services) { NeedsClarificationStatus = "needs-info" };
        await runner.RunAsync("code");

        var shown = harness.ChatClient.LastScriptedMessages.First(m => m.Role == Microsoft.Extensions.AI.ChatRole.User).Text;
        shown.Should().Contain(reply, "the reply reaches the derivation through the conversation");
        tickets.Finalized.Should().ContainSingle().Which.Status.Should().Be("needs-info",
            "a person replied and the model still sees a contradiction, so the ticket parks again");
    }

    /// <summary>
    /// 2026-10-12-3a36b, the shape of the run that started it: the derivation greps once and hands
    /// back a contradiction citing that grep; a second reading refutes it, so the derivation is asked
    /// again and cuts phases — the ticket is never parked on the false claim.
    /// </summary>
    [Fact]
    public async Task Contradiction_RefutedBySecondReading_IsDerivedAgainNotParked()
    {
        var tickets = new RecordingTicketProvider();
        await using var harness = BuildHarness(tickets);
        var checker = harness.Services.GetRequiredService<HarnessContradictionChecker>()
            .Refutes("the request also declares ShipmentId", "[C1] primary: the contradiction check ran 'read Request.cs' exited 0");
        harness.ChatClient
            .EnqueueToolCall(Application.Services.Specs.RepositorySearchTool.Name,
                """{"repository":"default","pattern":"SubmitAssetsRequest"}""")
            .EnqueueText("""
                {"phases": [], "handback": {"case": "requirements_contradict_repository",
                 "reason": "SubmitAssetsRequest contains only TruckPlate and TrailerPlate", "cites": ["L1"]}}
                """)
            .EnqueueText(SpecDerivationFixture.DerivationJson)
            .EnqueueText("Planning: I will patch the file.")
            .EnqueueToolCall("write_file", """{"path":"primary/src/Patch.cs","content":"// real fix"}""")
            .EnqueueText(GreenVerdict);

        var runner = new PipelineRunner(harness.Services) { NeedsClarificationStatus = "needs-info" };
        await runner.RunAsync("code");

        checker.Asked.Should().ContainSingle().Which.Cited.Should().ContainSingle()
            .Which.Should().StartWith("[L1] default:");
        harness.ChatClient.CallMessages.Should().Contain(call => call.Any(m => m.Text.Contains("refutes it")),
            "the derivation is answered with the refutation");
        runner.LastContext!.Get<SpecSet>(ContextKeys.SpecSet).Phases.Should().ContainSingle();
        tickets.Finalized.Should().NotContain(f => f.Status == "needs-info", "nothing parks on a refuted claim");
    }

    private const string GreenVerdict =
        """Done. {"status":"green","build_ran":true,"build_passed":true,"tests_ran":true,"tests_passed":true,"summary":"fixed","acceptance":[{"criterion":"criterion 1","status":"met","evidence":"handled"},{"criterion":"criterion 2","status":"met","evidence":"preserved"}]}""";

    private static SpecHandback Handback() => new(
        SpecHandbackCase.RequirementsContradictRepository,
        "The ticket renames a client this repository does not contain.");

    private static TicketComment OurContradiction() => new(
        "agent-smith", DateTimeOffset.UtcNow.AddHours(-2),
        Application.Services.Specs.SpecHandbackComment.Build(Handback(), null, string.Empty));

    private static SpecSet PreviousContradiction() => new(
        TicketKey.For("recording", "1").Value, [], SpecAccounting.Empty,
        [new SpecRevision(1, Application.Services.Specs.SpecRevisionCause.Initial, DateTimeOffset.UtcNow.AddHours(-2))],
        SpecSource.BranchArtifact, Handback());

    // The pointer this system recorded at the first park: the contradiction case, once.
    private static Task SeedPointerAsync(RealCompositionHarness harness) =>
        harness.Services.GetRequiredService<ISpecSetPointerStore>().SaveAsync(
            string.Empty,
            new SpecSetPointer(
                TicketKey.For("recording", "1").Value, "primary", SpecSha, 1,
                SpecHandbackCase.RequirementsContradictRepository, 1),
            CancellationToken.None);

    private static RealCompositionHarness BuildHarness(RecordingTicketProvider tickets, SpecSet? previous = null) =>
        RealCompositionHarness.Build(FixturePaths.For(FixturePaths.Default), services =>
        {
            HarnessProjectAnalyzerStub.Register(services);
            services.RemoveAll<ITicketProviderFactory>();
            services.AddSingleton<ITicketProviderFactory>(new RecordingTicketProviderFactory(tickets));
            if (previous is null) return;
            services.RemoveAll<ISpecSetReader>();
            services.AddSingleton<ISpecSetReader>(new SeededSpecSetReader(previous, SpecSha));
        });

    private sealed class SeededSpecSetReader(SpecSet set, string sha) : ISpecSetReader
    {
        public Task<SpecSetOnBranch> ReadAsync(
            PipelineContext pipeline, RepoConnection carryingRepo, TicketKey ticket,
            CancellationToken cancellationToken) =>
            Task.FromResult(SpecSetOnBranch.Answered(new SpecSetReadResult(set, sha)));
    }
}
