using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Specs;
using AgentSmith.Tests.TestHelpers;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.PipelineHarness.Composition;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// p0315d fast-tier coverage for the phase-driven scenario, through the REAL composition:
/// the ticket boundary is a recording fake; the LLM is scripted; the sandbox is the
/// staging-aware stub. Proves the spec-first path end-to-end — spec gate,
/// spec-as-approved-plan prompt, done-criteria contract, the specs/done/ dogfood record —
/// and the mid-run clarification park.
/// <para>
/// 2026-10-06-03c7d: a ticket's text is no spec source any more, so the spec lies on the
/// branch: it is seeded at the reader port, as the other branch-artifact presets do — the
/// fast tier's sandbox is fresh per run.
/// </para>
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class PhaseExecutionTests
{
    private const string ValidYaml =
        """
        spec: 2026-10-06-5e5ea
        goal: "Add a widget endpoint to the sample service"
        steps:
          - id: impl
            action: "Add the widget endpoint + handler"
        tests:
          - "Widget_Get_ReturnsWidget"
        done:
          - "GET /widget returns the widget"
        """;

    [Fact]
    public async Task PhaseExecution_RunsStepsThenVerifiesDoneCriteria()
    {
        // The p0317 comment thread: an operator answer posted while the ticket
        // was parked for clarification. FetchTicket hydrates it on the
        // re-triggered run, so the answer reaches the master even though the
        // comment-re-trigger path was status-gated out at post time (the
        // p0315d parked-while-answered residual, closed by this merge).
        var tickets = new PhaseTicketProvider(PhaseTicketBody(),
            comments:
            [
                new TicketComment(
                    "operator", new DateTimeOffset(2026, 7, 1, 9, 0, 0, TimeSpan.Zero),
                    "Answer to your question: use bearer-token auth for the widget endpoint"),
                // The cut comment the set's own publish posted after it: the answer is not input
                // the set has not seen, so the seeded spec is worked unamended.
                new TicketComment(
                    "agent-smith", new DateTimeOffset(2026, 7, 1, 10, 0, 0, TimeSpan.Zero),
                    AgentSmith.Application.Services.Specs.SpecSetComment.Render(SeededSet(), null)),
            ]);
        await using var harness = BuildHarness(tickets);
        harness.ChatClient
            // p0394a: no plan slot — the spec IS the plan; the master consumes first.
            .EnqueueToolCall("write_file", """{"path":"primary/src/Widget.cs","content":"// widget endpoint"}""")
            .EnqueueToolCall("run_command", """{"command":"dotnet test","repo":"primary"}""")
            .EnqueueToolCall("update_progress", """{"items":[{"id":"impl","activity":"Add the widget endpoint + handler","status":"done"}]}""")
            .EnqueueText("""All done criteria verified. {"status":"green","build_ran":true,"build_passed":true,"tests_ran":true,"tests_passed":true,"summary":"widget endpoint shipped","acceptance":[{"criterion":"criterion 1","status":"met","evidence":"handled in the change"},{"criterion":"criterion 2","status":"met","evidence":"existing behaviour preserved"}]}""");

        var runner = new PipelineRunner(harness.Services);
        var result = await runner.RunAsync("code");

        result.IsSuccess.Should().BeTrue(
            $"a real change + green verdict must pass the coding keystone: {result.Message}");

        // The spec drove the run: the user prompt carries the validated spec
        // verbatim (the yaml IS the requirement record) plus the spec-first
        // contract naming the done criteria to verify. The spec→plan-of-record
        // rendering goes through {PlanSection}, which the harness stub catalog
        // body does not declare — pinned at the unit level instead
        // (AgenticMasterPlanSectionTests over the production BuildPlanSection).
        var promptText = string.Join(
            "\n", harness.ChatClient.LastScriptedMessages.Select(m => m.Text ?? string.Empty));
        promptText.Should().Contain("spec: 2026-10-06-5e5ea",
            "the validated spec must reach the master verbatim");
        promptText.Should().Contain("Add the widget endpoint + handler",
            "the spec's steps are the work the master executes");
        promptText.Should().Contain("Done criteria");
        promptText.Should().Contain("GET /widget returns the widget",
            "the master must be told exactly which done criteria to verify");
        promptText.Should().Contain("Ticket conversation",
            "the hydrated comment thread must render into the phase prompt");
        promptText.Should().Contain("use bearer-token auth for the widget endpoint",
            "an answer commented while the ticket was parked must reach the re-triggered run");

        // Dogfood record: the executed spec lands in specs/done/ inside the
        // sandbox working tree, riding the same commit CommitAndPR ships.
        var wroteRecord = harness.StubSandboxFactory!.Spawned
            .SelectMany(s => s.Sandbox.RanSteps)
            .Any(s => s.Kind == AgentSmith.Sandbox.Wire.StepKind.WriteFile
                && s.Path is { } p
                && p.Contains(".agentsmith/specs/done/", StringComparison.Ordinal)
                && p.EndsWith("2026-10-06-5e5ea-add-a-widget-endpoint-to-the-sample-service.yaml", StringComparison.Ordinal));
        wroteRecord.Should().BeTrue(
            "the phase yaml must be written to .agentsmith/specs/done/ in the sandbox tree");

        harness.ChatClient.ToolCalls.ShouldHaveCalledInOrder("write_file", "run_command", "update_progress");

        // 2026-09-17-042eh: the verified phase's own diff reaches a fresh instance — the step
        // is in the block, so a preset that ships code asks this question once per phase.
        harness.ChatClient.PromptsSeen.Should().Contain(
            p => p.Contains(AgentSmith.PipelineHarness.Llm.PhaseReviewScript.Marker, StringComparison.Ordinal),
            "the phase review runs after the verification and before the record");
    }

    [Fact]
    public async Task PhaseExecution_MasterNeedsInput_MovesTicketToNeedsClarification()
    {
        var tickets = new PhaseTicketProvider(PhaseTicketBody());
        await using var harness = BuildHarness(tickets);
        harness.ChatClient
            // p0394a: no plan slot — the master consumes first.
            .EnqueueToolCall("ask_human", """{"question":"Which auth scheme should the widget endpoint use?"}""")
            .EnqueueText("Waiting for the operator's answer.");

        var runner = new PipelineRunner(harness.Services) { NeedsClarificationStatus = "Question" };
        var result = await runner.RunAsync("code");

        result.IsSuccess.Should().BeTrue("a clarification park is an incomplete run, not a failure");
        result.Message.Should().Contain("awaiting_user_input",
            "the executor must record the run as parked for input");

        // The question reached the ticket via the p0318 transport: one atomic
        // comment + native status move into needs_clarification_status.
        var park = tickets.Finalized.Should().ContainSingle(
            "the master's question must park the ticket in one provider call").Subject;
        park.Status.Should().Be("Question");
        park.Comment.Should().Contain("Which auth scheme should the widget endpoint use?");
        park.Comment.Should().Contain("agent-smith",
            "the comment must carry the open-questions marker the answer parser keys on");

        // A parked run ships nothing: no specs/done record, no PR path.
        harness.StubSandboxFactory!.Spawned
            .SelectMany(s => s.Sandbox.RanSteps)
            .Should().NotContain(s => s.Kind == AgentSmith.Sandbox.Wire.StepKind.WriteFile
                && s.Path != null && s.Path.Contains(".agentsmith/specs/done/", StringComparison.Ordinal),
                "a run parked for clarification must not record the phase as done");
    }

    private static RealCompositionHarness BuildHarness(PhaseTicketProvider tickets) =>
        RealCompositionHarness.Build(FixturePaths.For(FixturePaths.Default), services =>
        {
            services.RemoveAll<ISpecSetReader>();
            services.AddSingleton<ISpecSetReader>(new SeededSpecSetReader(SeededSet(), "seeded-sha"));
            // The analyzer would drain the scripted FIFO at AnalyzeCode (same
            // reason FixBug's keystone tests stub it).
            HarnessProjectAnalyzerStub.Register(services);
            // The ticket tracker HTTP boundary: a recording provider that serves
            // the phase ticket and captures the park call.
            services.RemoveAll<ITicketProviderFactory>();
            services.AddSingleton<ITicketProviderFactory>(new PhaseTicketProviderFactory(tickets));
        });

    private static string PhaseTicketBody() => "## Goal\nAdd a widget endpoint to the sample service\n";

    // The spec as the branch carries it: one planned spec of a series, no pointer recorded, so
    // the run works it unamended.
    private static SpecSet SeededSet() => new(
        TicketKey.For("recording", "1").Value,
        [new SpecPhase(new PhaseDraftReader().Read(ValidYaml), "add-a-widget-endpoint-to-the-sample-service",
            string.Empty, [])],
        SpecAccounting.Empty,
        [new SpecRevision(1, "initial derivation", DateTimeOffset.UtcNow.AddHours(-1))],
        SpecSource.BranchArtifact,
        Series: "2026-10-06-5e5e");

    private sealed class SeededSpecSetReader(SpecSet set, string sha) : ISpecSetReader
    {
        public Task<SpecSetOnBranch> ReadAsync(
            PipelineContext pipeline, RepoConnection carryingRepo, TicketKey ticket,
            CancellationToken cancellationToken) =>
            Task.FromResult(SpecSetOnBranch.Answered(new SpecSetReadResult(set, sha)));
    }

    private sealed class PhaseTicketProvider(
        string body, IReadOnlyList<TicketComment>? comments = null) : ITicketProvider
    {
        private readonly List<(TicketId Id, string Comment, string? Status)> _finalized = [];

        public IReadOnlyList<(TicketId Id, string Comment, string? Status)> Finalized
        {
            get { lock (_finalized) return [.. _finalized]; }
        }

        public string ProviderType => "recording";

        public Task<ConnectionProbeResult> ProbeAsync(CancellationToken cancellationToken) =>
            Task.FromResult(ConnectionProbeResult.Reachable(0));

        public Task<Ticket> GetTicketAsync(TicketId ticketId, CancellationToken cancellationToken) =>
            Task.FromResult(new Ticket(
                ticketId, "p9999: Add a widget endpoint to the sample service",
                body, null, "Open", "recording", ["phase"]));

        public Task<CreatedTicket> CreateAsync(
            string title, string description, IReadOnlyList<string> labels, string? kind,
            CancellationToken cancellationToken) =>
            Task.FromResult(new CreatedTicket(new TicketId("1"), "https://tracker.test/1"));

        public Task<IReadOnlyList<TicketComment>> GetCommentsAsync(
            TicketId ticketId, CancellationToken cancellationToken) =>
            Task.FromResult(comments ?? []);

        public Task<TicketFinalizeResult> FinalizeAsync(
            TicketId ticketId, string comment, string? doneStatus, CancellationToken cancellationToken)
        {
            lock (_finalized) _finalized.Add((ticketId, comment, doneStatus));
            return Task.FromResult(TicketFinalizeResult.Moved());
        }
    }

    private sealed class PhaseTicketProviderFactory(PhaseTicketProvider provider) : ITicketProviderFactory
    {
        public ITicketProvider Create(TrackerConnection config) => provider;

        public ITicketRewriter CreateRewriter(TrackerConnection config) => new HarnessTicketRewriter();

        public ITicketSearch CreateSearch(TrackerConnection config) => new HarnessTicketSearch();

        public ITicketLinkedWork CreateLinkedWork(TrackerConnection config) =>
            new RecordingLinkedWork();
    }
}
