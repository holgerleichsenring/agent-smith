using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Models;
using AgentSmith.PipelineHarness.Composition;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// 2026-09-08-5cd2 fast-tier end-to-end, run a109's shape: the branch carries a two-phase
/// set whose first phase ran (the marker's commit is the last on the spec path, so the
/// pointer's sha no longer matches) and the ticket comes back re-triggered. Edited since
/// the cut, the derivation is called again with the set, keeps the executed phase, re-cuts
/// the tail from the current text, only the tail runs, and the ticket is told. Unchanged,
/// the tail continues exactly as it was cut and nothing is derived.
/// <para>
/// The branch artifact is stood in for at its port, as the question tests do — the fast
/// tier's sandbox is fresh per run.
/// </para>
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class TicketEditRecutTests
{
    private const string SpecSha = "spec-sha-1";
    private const string MarkerSha = "marker-sha-2";
    private const string TailAsCut = "Move the callers, as cut before the edit";

    // The ids the derivation mints for the fixture ticket — the executed head is carried
    // by position, so the seeded set must use the same scheme the parser does.
    private static readonly string HeadId = PhaseIdFactory.For("1", 0);
    private static readonly string TailId = PhaseIdFactory.For("1", 1);

    private const string GreenVerdict =
        """Done. {"status":"green","build_ran":true,"build_passed":true,"tests_ran":true,"tests_passed":true,"summary":"fixed","acceptance":[{"criterion":"criterion 1","status":"met","evidence":"handled"}]}""";

    [Fact]
    public async Task TicketEdit_ADonePhaseAndAnEditedDescription_RecutsTheTailAndSaysSo()
    {
        var tickets = new RecordingTicketProvider();
        await using var harness = BuildHarness(tickets, Previous(fingerprint: "the-text-before-the-edit"));
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
        set.Current.Cause.Should().Be(SpecRevisionCause.TicketEdit);
        set.Phases.Select(p => p.PhaseId).Should().Equal(HeadId, TailId);
        set.Phases[0].Draft.Goal.Should().Be("Introduce the guard", "the executed phase is carried, not re-read from the reply");
        set.Phases[1].Draft.Goal.Should().Be("Move the existing callers onto the guard", "the tail is cut again from the current text");
        harness.ChatClient.ToolCalls.ShouldHaveCalledInOrder("write_file");
        tickets.Commented.Should().ContainSingle(c => c.Comment.Contains(
            $"The ticket text changed since revision 1 was cut: {HeadId} already ran and stayed as it was"));
    }

    [Fact]
    public async Task TicketEdit_ADonePhaseAndAnUnchangedDescription_ContinuesTheTailUnchanged()
    {
        var tickets = new RecordingTicketProvider();
        var current = await tickets.GetTicketAsync(new TicketId("1"), CancellationToken.None);
        await using var harness = BuildHarness(tickets, Previous(TicketTextFingerprint.Of(current)));
        await SeedPointerAsync(harness);
        harness.ChatClient
            .EnqueueText("Planning: move the callers.")
            .EnqueueToolCall("write_file", """{"path":"primary/src/Callers.cs","content":"// moved"}""")
            .EnqueueText(GreenVerdict);

        var runner = new PipelineRunner(harness.Services);
        var result = await runner.RunAsync("code");

        result.IsSuccess.Should().BeTrue(result.Message);
        var set = runner.LastContext!.Get<SpecSet>(ContextKeys.SpecSet);
        set.Current.Cause.Should().NotBe(SpecRevisionCause.TicketEdit, "the text the set was cut from is the text on the ticket");
        set.Phases[1].Draft.Goal.Should().Be(TailAsCut, "nothing was derived");
        harness.ChatClient.ToolCalls.ShouldHaveCalledInOrder("write_file");
        tickets.Commented.Should().NotContain(c => c.Comment.Contains("The ticket text changed"));
    }

    private static SpecSet Previous(string? fingerprint) => new(
        SpecSetKey.For("recording", "1").Value,
        [Phase(HeadId, "Introduce the guard"), Phase(TailId, TailAsCut)],
        SpecAccounting.Empty,
        [new SpecRevision(1, SpecRevisionCause.Initial, DateTimeOffset.UtcNow.AddHours(-2))],
        SpecSource.BranchArtifact,
        ExecutedPhaseIds: [HeadId],
        TicketFingerprint: fingerprint);

    private static SpecPhase Phase(string id, string goal) => new(
        new PhaseDraft(id, goal, $"phase: {id}\ngoal: \"{goal}\"\ndone:\n  - \"criterion 1\"\n", [])
        {
            Done = ["criterion 1"],
        },
        PhaseIdFactory.Slug(goal), string.Empty, [1, 2, 3, 4, 5, 6]);

    // The pointer this system recorded at the derivation commit; the marker's commit
    // since moved the last sha on the spec path, which is what a109 saw.
    private static Task SeedPointerAsync(RealCompositionHarness harness) =>
        harness.Services.GetRequiredService<ISpecSetPointerStore>().SaveAsync(
            string.Empty,
            new SpecSetPointer(SpecSetKey.For("recording", "1").Value, "primary", SpecSha, 1),
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
