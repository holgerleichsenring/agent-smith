using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
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
/// 2026-09-08-4aa9 fast-tier end-to-end, run a109's other half: the branch carries a
/// two-phase set whose first phase ran, the marker moved the pointer to its own commit,
/// the ticket text is unchanged, and the ticket comes back re-triggered. With an operator
/// comment after our derivation-time comment, the derivation is called again with the
/// set, keeps the executed phase, re-cuts the tail with the comment in view, only the
/// tail runs, and the ticket is told. With no comment, the tail continues exactly as it
/// was cut and nothing is derived.
/// <para>
/// The branch artifact is stood in for at its port, as the ticket-edit tests do — the
/// fast tier's sandbox is fresh per run.
/// </para>
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class CommentRecutTests
{
    private const string MarkerSha = "marker-sha-2";
    private const string TailAsCut = "Move the callers, as cut before the comment";

    // The ids the derivation mints for the fixture ticket — the executed head is carried
    // by position, so the seeded set must use the same scheme the parser does.
    private static readonly string HeadId = PhaseIdFactory.For("1", 0);
    private static readonly string TailId = PhaseIdFactory.For("1", 1);

    private const string GreenVerdict =
        """Done. {"status":"green","build_ran":true,"build_passed":true,"tests_ran":true,"tests_passed":true,"summary":"fixed","acceptance":[{"criterion":"criterion 1","status":"met","evidence":"handled"}]}""";

    [Fact]
    public async Task CommentRecut_ADonePhaseAndAnOperatorComment_RecutsTheTailAndSaysSo()
    {
        var tickets = new RecordingTicketProvider([OurCut(), OperatorObjection()]);
        await using var harness = BuildHarness(tickets, await PreviousAsync(tickets));
        await SeedPointerAsync(harness);
        harness.ChatClient
            .EnqueueText(SpecDerivationFixture.TwoPhaseJson)
            .EnqueueText("Planning: move the callers.")
            .EnqueueToolCall("write_file", """{"path":"primary/src/Callers.cs","content":"// moved"}""")
            .EnqueueText(GreenVerdict);

        var runner = new PipelineRunner(harness.Services);
        var result = await runner.RunAsync("code");

        result.IsSuccess.Should().BeTrue(result.Message);
        var set = runner.LastContext!.Get<SpecSet>(ContextKeys.SpecSet);
        set.Current.Cause.Should().Be(SpecRevisionCause.Comment);
        set.Phases.Select(p => p.PhaseId).Should().Equal(HeadId, TailId);
        set.Phases[0].Draft.Goal.Should().Be("Introduce the guard", "the executed phase is carried, not re-read from the reply");
        set.Phases[1].Draft.Goal.Should().Be("Move the existing callers onto the guard", "the tail is cut again with the comment in view");
        harness.ChatClient.ToolCalls.ShouldHaveCalledInOrder("write_file");
        tickets.Commented.Should().ContainSingle(c => c.Comment.Contains(
            $"The ticket was commented on after revision 1 was cut: {HeadId} already ran and stayed as it was"));
    }

    [Fact]
    public async Task CommentRecut_ADonePhaseAndNoComment_ContinuesTheTailWithoutTheModel()
    {
        var tickets = new RecordingTicketProvider([OurCut()]);
        await using var harness = BuildHarness(tickets, await PreviousAsync(tickets));
        await SeedPointerAsync(harness);
        harness.ChatClient
            .EnqueueText("Planning: move the callers.")
            .EnqueueToolCall("write_file", """{"path":"primary/src/Callers.cs","content":"// moved"}""")
            .EnqueueText(GreenVerdict);

        var runner = new PipelineRunner(harness.Services);
        var result = await runner.RunAsync("code");

        result.IsSuccess.Should().BeTrue(result.Message);
        var set = runner.LastContext!.Get<SpecSet>(ContextKeys.SpecSet);
        set.Current.Cause.Should().Be(SpecRevisionCause.Retrigger, "the marker's commit is this system's own");
        set.Phases[1].Draft.Goal.Should().Be(TailAsCut, "nothing was derived");
        harness.ChatClient.ToolCalls.ShouldHaveCalledInOrder("write_file");
        tickets.Commented.Should().NotContain(c => c.Comment.Contains("was commented on"));
    }

    // The set the previous run cut from the ticket as it still reads, its first phase run.
    private static async Task<SpecSet> PreviousAsync(RecordingTicketProvider tickets)
    {
        var current = await tickets.GetTicketAsync(new TicketId("1"), CancellationToken.None);
        return Previous(TicketTextFingerprint.Of(current));
    }

    private static SpecSet Previous(string? fingerprint) => new(
        SpecSetKey.For("recording", "1").Value,
        [Phase(HeadId, "Introduce the guard"), Phase(TailId, TailAsCut)],
        SpecAccounting.Empty,
        [new SpecRevision(1, SpecRevisionCause.Initial, DateTimeOffset.UtcNow.AddHours(-3))],
        SpecSource.BranchArtifact,
        ExecutedPhaseIds: [HeadId],
        TicketFingerprint: fingerprint);

    private static SpecPhase Phase(string id, string goal) => new(
        new PhaseDraft(id, goal, $"phase: {id}\ngoal: \"{goal}\"\ndone:\n  - \"criterion 1\"\n", [])
        {
            Done = ["criterion 1"],
        },
        PhaseIdFactory.Slug(goal), string.Empty, [1, 2, 3, 4, 5, 6]);

    // What the previous run posted when it cut the set, and what the operator said after.
    private static TicketComment OurCut() => new(
        "agent-smith", DateTimeOffset.UtcNow.AddHours(-2), SpecSetComment.Render(Previous(null), null));

    private static TicketComment OperatorObjection() => new(
        "operator", DateTimeOffset.UtcNow.AddHours(-1),
        "The second phase is wrong: the callers should be moved onto the guard, not rewritten.");

    // The pointer as the marker left it: at its own commit, the last on the spec path.
    private static Task SeedPointerAsync(RealCompositionHarness harness) =>
        harness.Services.GetRequiredService<ISpecSetPointerStore>().SaveAsync(
            string.Empty,
            new SpecSetPointer(SpecSetKey.For("recording", "1").Value, "primary", MarkerSha, 1),
            CancellationToken.None);

    private static RealCompositionHarness BuildHarness(RecordingTicketProvider tickets, SpecSet previous) =>
        RealCompositionHarness.Build(FixturePaths.For(FixturePaths.Default), services =>
        {
            HarnessProjectAnalyzerStub.Register(services);
            services.RemoveAll<ITicketProviderFactory>();
            services.AddSingleton<ITicketProviderFactory>(new RecordingTicketProviderFactory(tickets));
            services.RemoveAll<ISpecSetReader>();
            services.AddSingleton<ISpecSetReader>(new SeededSpecSetReader(previous, MarkerSha));
        });

    private sealed class SeededSpecSetReader(SpecSet set, string sha) : ISpecSetReader
    {
        public Task<SpecSetReadResult?> ReadAsync(
            PipelineContext pipeline, RepoConnection carryingRepo, SpecSetKey key,
            CancellationToken cancellationToken) =>
            Task.FromResult<SpecSetReadResult?>(new SpecSetReadResult(set, sha));
    }
}
