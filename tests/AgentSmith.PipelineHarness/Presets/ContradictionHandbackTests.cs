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
    // nothing built and the run reported green. (The p0237 finalizer tail still posts that
    // record-PR summary on a failed run before the failure comment lands — a defect of its
    // own, named in the phase's scope, so this test pins the failure and the last word.)
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
        tickets.Finalized.Last().Comment.Should().Contain("Agent Smith — Failed").And.Contain("the loop ends here",
            "the failure, with its reason, is the ticket's last word");
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

        var shown = harness.ChatClient.LastMessages.First(m => m.Role == Microsoft.Extensions.AI.ChatRole.User).Text;
        shown.Should().Contain(reply, "the reply reaches the derivation through the conversation");
        tickets.Finalized.Should().ContainSingle().Which.Status.Should().Be("needs-info",
            "a person replied and the model still sees a contradiction, so the ticket parks again");
    }

    private static SpecHandback Handback() => new(
        SpecHandbackCase.RequirementsContradictRepository,
        "The ticket renames a client this repository does not contain.");

    private static TicketComment OurContradiction() => new(
        "agent-smith", DateTimeOffset.UtcNow.AddHours(-2),
        Application.Services.Specs.SpecHandbackComment.Build(Handback(), null, string.Empty));

    private static SpecSet PreviousContradiction() => new(
        SpecSetKey.For("recording", "1").Value, [], SpecAccounting.Empty,
        [new SpecRevision(1, Application.Services.Specs.SpecRevisionCause.Initial, DateTimeOffset.UtcNow.AddHours(-2))],
        SpecSource.BranchArtifact, Handback());

    // The pointer this system recorded at the first park: the contradiction case, once.
    private static Task SeedPointerAsync(RealCompositionHarness harness) =>
        harness.Services.GetRequiredService<ISpecSetPointerStore>().SaveAsync(
            string.Empty,
            new SpecSetPointer(
                SpecSetKey.For("recording", "1").Value, "primary", SpecSha, 1,
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
        public Task<SpecSetReadResult?> ReadAsync(
            PipelineContext pipeline, RepoConnection carryingRepo, SpecSetKey key,
            CancellationToken cancellationToken) =>
            Task.FromResult<SpecSetReadResult?>(new SpecSetReadResult(set, sha));
    }
}
