using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Entities;
using AgentSmith.PipelineHarness.Composition;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// 2026-09-07-c9d4 fast-tier end-to-end: a ticket that reads two ways parks as a
/// question naming both readings and the one the run would take; re-triggered with
/// nobody having answered, the derivation is pinned to that reading, cuts real phases,
/// and the ticket is told which reading the run proceeded on.
/// <para>
/// The second run reads the previous set through a seeded reader: the fast tier's
/// sandbox is fresh per run, so the branch artifact is stood in for at its port, the
/// way the tracker is.
/// </para>
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class QuestionHandbackTests
{
    private const string ReadingA = "a major only where nothing lower clears the advisory";
    private const string ReadingB = "the newest major everywhere, breaking changes included";
    private const string SpecSha = "spec-sha-1";

    private const string GreenVerdict =
        """Done. {"status":"green","build_ran":true,"build_passed":true,"tests_ran":true,"tests_passed":true,"summary":"fixed","acceptance":[{"criterion":"criterion 1","status":"met","evidence":"handled"},{"criterion":"criterion 2","status":"met","evidence":"preserved"}]}""";

    [Fact]
    public async Task Question_ATicketWithTwoReadings_ParksWithBothAndTheOneTaken()
    {
        var tickets = new RecordingTicketProvider();
        await using var harness = BuildHarness(tickets);
        harness.ChatClient.EnqueueText(SpecDerivationFixture.QuestionJson);

        var runner = new PipelineRunner(harness.Services) { NeedsClarificationStatus = "needs-info" };
        var result = await runner.RunAsync("code");

        result.IsSuccess.Should().BeTrue("a question parks the run; it is not a failed step");
        result.Message.Should().Contain("awaiting_user_input");
        runner.LastContext!.Get<bool>(ContextKeys.OpenQuestionsAwaitingAnswer).Should().BeTrue();
        runner.LastContext.Get<SpecHandback>(ContextKeys.SpecHandback).Case.Should().Be(SpecHandbackCase.Question);
        harness.ChatClient.ToolCalls.Should().BeEmpty("nothing is built on a reading nobody chose");

        var park = tickets.Finalized.Should().ContainSingle().Subject;
        park.Status.Should().Be("needs-info", "a question parks where a person can answer");
        park.Comment.Should().Contain("(a) " + ReadingA).And.Contain("(b) " + ReadingB);
        park.Comment.Should().Contain("proceeds on (a)", "the ticket says which door the run goes through if nobody answers");
    }

    [Fact]
    public async Task Question_ASecondUnansweredPark_ProceedsAndSaysOnWhichReading()
    {
        var tickets = new RecordingTicketProvider([OurQuestion()]);
        await using var harness = BuildHarness(tickets, previous: PreviousQuestion());
        await SeedPointerAsync(harness);
        harness.ChatClient
            .EnqueueText(SpecDerivationFixture.DerivationJson)
            .EnqueueText("Planning: bump the packages.")
            .EnqueueToolCall("write_file", """{"path":"primary/src/Packages.props","content":"// bumped"}""")
            .EnqueueText(GreenVerdict);

        var runner = new PipelineRunner(harness.Services) { NeedsClarificationStatus = "needs-info" };
        await runner.RunAsync("code");

        tickets.Finalized.Should().NotContain(f => f.Status == "needs-info", "nobody answered, so the run proceeds");
        runner.LastContext!.Get<SpecSet>(ContextKeys.SpecSet).Phases.Should().ContainSingle("real phases, cut under the taken reading");
        harness.ChatClient.ToolCalls.ShouldHaveCalledInOrder("write_file");
        var notice = tickets.Commented.Should().ContainSingle(c => c.Comment.Contains("proceeding on reading (a)")).Subject;
        notice.Comment.Should().Contain("> (a) " + ReadingA, "the reading is named from the question that was asked");
    }

    [Fact]
    public async Task Question_AnAnsweredQuestion_IsDerivedOnTheAnswerWithoutAPin()
    {
        var answer = "(b) — the newest major everywhere, please.";
        var tickets = new RecordingTicketProvider(
        [
            OurQuestion(),
            new TicketComment("operator", DateTimeOffset.UtcNow.AddHours(-1), answer),
        ]);
        await using var harness = BuildHarness(tickets, previous: PreviousQuestion());
        await SeedPointerAsync(harness);
        // The model asks AGAIN despite the answer, so the derivation call is the run's last
        // call and its prompt is what the model was shown.
        harness.ChatClient.EnqueueText(SpecDerivationFixture.QuestionJson);

        var runner = new PipelineRunner(harness.Services) { NeedsClarificationStatus = "needs-info" };
        await runner.RunAsync("code");

        var shown = harness.ChatClient.LastMessages.First(m => m.Role == ChatRole.User).Text;
        shown.Should().Contain(answer, "the answer reaches the derivation through the conversation");
        shown.Should().NotContain("was left unanswered", "an answered question is not pinned");
        tickets.Finalized.Should().ContainSingle().Which.Status.Should().Be("needs-info",
            "the model asked again, so the ticket parks again");
        tickets.Commented.Should().NotContain(c => c.Comment.Contains("proceeding on reading"));
    }

    [Fact]
    public async Task Question_TheModelAsksAgainDespiteThePin_ParksAgainAndThePinWasShown()
    {
        var tickets = new RecordingTicketProvider([OurQuestion()]);
        await using var harness = BuildHarness(tickets, previous: PreviousQuestion());
        await SeedPointerAsync(harness);
        harness.ChatClient.EnqueueText(SpecDerivationFixture.QuestionJson);

        var runner = new PipelineRunner(harness.Services) { NeedsClarificationStatus = "needs-info" };
        await runner.RunAsync("code");

        var shown = harness.ChatClient.LastMessages.First(m => m.Role == ChatRole.User).Text;
        shown.Should().Contain("## The question from the last run was left unanswered");
        shown.Should().Contain("Reading (a) is").And.Contain("(b) " + ReadingB);
        tickets.Finalized.Should().ContainSingle().Which.Status.Should().Be("needs-info");
        tickets.Commented.Should().NotContain(c => c.Comment.Contains("proceeding on reading"));
    }

    private static TicketComment OurQuestion() => new(
        "agent-smith", DateTimeOffset.UtcNow.AddHours(-2),
        Application.Services.Specs.SpecHandbackComment.Build(PreviousQuestion().Handback!, null, string.Empty));

    private static SpecSet PreviousQuestion() => new(
        SpecSetKey.For("recording", "1").Value, [], SpecAccounting.Empty,
        [new SpecRevision(1, Application.Services.Specs.SpecRevisionCause.Initial, DateTimeOffset.UtcNow.AddHours(-2))],
        SpecSource.BranchArtifact,
        new SpecHandback(SpecHandbackCase.Question, "reads two ways", Readings: [ReadingA, ReadingB], Taken: 0));

    // The pointer this system would have recorded when it committed the question: same
    // sha as the branch, so the re-run reads as a re-trigger and calls the model.
    private static Task SeedPointerAsync(RealCompositionHarness harness) =>
        harness.Services.GetRequiredService<ISpecSetPointerStore>().SaveAsync(
            string.Empty,
            new SpecSetPointer(
                SpecSetKey.For("recording", "1").Value, "primary", SpecSha, 1,
                SpecHandbackCase.Question, 1, SpecSha),
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
