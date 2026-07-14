using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.PipelineHarness.Composition;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// p0315d fast-tier phase-execution coverage through the REAL composition:
/// the ticket boundary is a recording fake returning a genuine p0315c phase
/// ticket (markdown summary + ONE fenced yaml spec, rendered by the
/// production PhaseTicketRenderer); the LLM is scripted; the sandbox is the
/// staging-aware stub. Proves the spec-first path end-to-end — extraction
/// gate, spec-as-approved-plan prompt, done-criteria contract, the
/// phases/done/ dogfood record — and the mid-run clarification park.
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class PhaseExecutionTests
{
    private const string ValidYaml =
        """
        phase: p9999
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
            ]);
        await using var harness = BuildHarness(tickets);
        harness.ChatClient
            .EnqueueToolCall("write_file", """{"path":"primary/src/Widget.cs","content":"// widget endpoint"}""")
            .EnqueueToolCall("run_command", """{"command":"dotnet test","repo":"primary"}""")
            .EnqueueText("""All done criteria verified. {"status":"green","build_ran":true,"build_passed":true,"tests_ran":true,"tests_passed":true,"summary":"widget endpoint shipped","acceptance":[{"criterion":"criterion 1","status":"met","evidence":"handled in the change"},{"criterion":"criterion 2","status":"met","evidence":"existing behaviour preserved"}]}""");

        var runner = new PipelineRunner(harness.Services);
        var result = await runner.RunAsync("phase-execution");

        result.IsSuccess.Should().BeTrue(
            $"a real change + green verdict must pass the phase-execution keystone: {result.Message}");

        // The spec drove the run: the user prompt carries the validated spec
        // verbatim (the yaml IS the requirement record) plus the spec-first
        // contract naming the done criteria to verify. The steps→approved-plan
        // system-prompt rendering goes through {PlanSection}, which the harness
        // stub catalog body does not declare — pinned at the unit level instead
        // (PhaseSpecPlanFactoryTests over the production BuildPlanSection).
        var promptText = string.Join(
            "\n", harness.ChatClient.LastMessages.Select(m => m.Text ?? string.Empty));
        promptText.Should().Contain("phase: p9999",
            "the validated spec must reach the master verbatim");
        promptText.Should().Contain("Add the widget endpoint + handler",
            "the spec's steps are the work the master executes");
        promptText.Should().Contain("Done criteria");
        promptText.Should().Contain("GET /widget returns the widget",
            "the master must be told exactly which done criteria to verify");
        promptText.Should().Contain("Ticket conversation",
            "the hydrated comment thread must render into the phase-execution prompt");
        promptText.Should().Contain("use bearer-token auth for the widget endpoint",
            "an answer commented while the ticket was parked must reach the re-triggered run");

        // Dogfood record: the executed spec lands in phases/done/ inside the
        // sandbox working tree, riding the same commit CommitAndPR ships.
        var wroteRecord = harness.StubSandboxFactory!.Spawned
            .SelectMany(s => s.Sandbox.RanSteps)
            .Any(s => s.Kind == AgentSmith.Sandbox.Wire.StepKind.WriteFile
                && s.Path is { } p
                && p.Contains(".agentsmith/phases/done/", StringComparison.Ordinal)
                && p.EndsWith("p9999-add-a-widget-endpoint-to-the-sample-service.yaml", StringComparison.Ordinal));
        wroteRecord.Should().BeTrue(
            "the phase yaml must be written to .agentsmith/phases/done/ in the sandbox tree");

        harness.ChatClient.ToolCalls.ShouldHaveCalledInOrder("write_file", "run_command");
    }

    [Fact]
    public async Task PhaseExecution_MasterNeedsInput_MovesTicketToNeedsClarification()
    {
        var tickets = new PhaseTicketProvider(PhaseTicketBody());
        await using var harness = BuildHarness(tickets);
        harness.ChatClient
            .EnqueueToolCall("ask_human", """{"question":"Which auth scheme should the widget endpoint use?"}""")
            .EnqueueText("Waiting for the operator's answer.");

        var runner = new PipelineRunner(harness.Services) { NeedsClarificationStatus = "Question" };
        var result = await runner.RunAsync("phase-execution");

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

        // A parked run ships nothing: no phases/done record, no PR path.
        harness.StubSandboxFactory!.Spawned
            .SelectMany(s => s.Sandbox.RanSteps)
            .Should().NotContain(s => s.Kind == AgentSmith.Sandbox.Wire.StepKind.WriteFile
                && s.Path != null && s.Path.Contains(".agentsmith/phases/done/", StringComparison.Ordinal),
                "a run parked for clarification must not record the phase as done");
    }

    private static RealCompositionHarness BuildHarness(PhaseTicketProvider tickets) =>
        RealCompositionHarness.Build(FixturePaths.For(FixturePaths.Default), services =>
        {
            // The analyzer would drain the scripted FIFO at AnalyzeCode (same
            // reason FixBug's keystone tests stub it).
            HarnessProjectAnalyzerStub.Register(services);
            // The ticket tracker HTTP boundary: a recording provider that serves
            // the phase ticket and captures the park call.
            services.RemoveAll<ITicketProviderFactory>();
            services.AddSingleton<ITicketProviderFactory>(new PhaseTicketProviderFactory(tickets));
        });

    // The genuine p0315c artifact: title + markdown summary + ONE fenced yaml
    // block, rendered by the production renderer (not a hand-built body).
    private static string PhaseTicketBody() =>
        new PhaseTicketRenderer()
            .RenderPhase(new PhaseDraft(
                "p9999", "Add a widget endpoint to the sample service", ValidYaml, []))
            .Body;

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
            string title, string description, IReadOnlyList<string> labels,
            CancellationToken cancellationToken) =>
            Task.FromResult(new CreatedTicket(new TicketId("1"), "https://tracker.test/1"));

        public Task<IReadOnlyList<TicketComment>> GetCommentsAsync(
            TicketId ticketId, CancellationToken cancellationToken) =>
            Task.FromResult(comments ?? []);

        public Task FinalizeAsync(
            TicketId ticketId, string comment, string? doneStatus, CancellationToken cancellationToken)
        {
            lock (_finalized) _finalized.Add((ticketId, comment, doneStatus));
            return Task.CompletedTask;
        }
    }

    private sealed class PhaseTicketProviderFactory(PhaseTicketProvider provider) : ITicketProviderFactory
    {
        public ITicketProvider Create(TrackerConnection config) => provider;
    }
}
