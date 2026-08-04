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
/// p0391 fast-tier proof that the master's way OUT actually works on the ticket-triggered
/// CODE presets, through the real composition.
///
/// Before this phase ask_human resolved to <c>HumanToolHost</c> on fix-bug / add-feature /
/// fix-no-test. That host needs a dialogue transport AND a job id, and
/// <c>ContextKeys.DialogueJobId</c> is set in exactly one place in the codebase
/// (SpecDialogTurnRunner) — so a ticket-triggered coding run never had one and the tool
/// answered the literal string "Error: Dialogue transport not configured.", while the deployed
/// skill instructed the master twice to call ask_human and stop. The prompt promised a door
/// that was bricked up; what the model had left was to keep working.
///
/// These assert the door is real: the question reaches the TICKET (one atomic comment + native
/// status move) and the run ENDS parked — not a captured question nobody reads.
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class MasterAskHumanParkTests
{
    [Fact]
    public async Task AskHuman_FixBugRun_ParksTheTicketInsteadOfReturningTransportError()
    {
        var tickets = new RecordingTicketProvider();
        await using var harness = BuildHarness(tickets);
        harness.ChatClient
            // p0390: DeriveSpecification runs before the master and drains one FIFO slot.
            // p0394a: no plan slot — the spec is the plan, the master consumes next.
            .EnqueueText(SpecDerivationFixture.DerivationJson)
            .EnqueueToolCall("ask_human",
                """{"question":"Should the refresh window be 5 or 15 minutes?"}""")
            .EnqueueText("Waiting for the operator's answer.");

        var runner = new PipelineRunner(harness.Services) { NeedsClarificationStatus = "Question" };
        var result = await runner.RunAsync("fix-bug");

        result.IsSuccess.Should().BeTrue("a clarification park is an incomplete run, not a failure");
        result.Message.Should().Contain("awaiting_user_input");

        // The tool's own reply is what the model reads next — it must not be the old error.
        var reply = harness.ChatClient.ToolCalls.First("ask_human");
        reply.Should().NotBeNull();

        var park = tickets.Finalized.Should().ContainSingle(
            "the master's question must park the ticket in one provider call").Subject;
        park.Status.Should().Be("Question", "the ticket must move to needs_clarification_status");
        park.Comment.Should().Contain("Should the refresh window be 5 or 15 minutes?");
        park.Comment.Should().Contain("agent-smith",
            "the comment must carry the open-questions marker the answer parser keys on");
    }

    [Fact]
    public async Task AskHuman_AddFeatureRun_QuestionIsPostedAndTheRunEnds()
    {
        var tickets = new RecordingTicketProvider();
        await using var harness = BuildHarness(tickets);
        harness.ChatClient
            // p0390: DeriveSpecification runs before the master and drains one FIFO slot.
            // p0394a: no plan slot — the spec is the plan, the master consumes next.
            .EnqueueText(SpecDerivationFixture.DerivationJson)
            .EnqueueToolCall("ask_human",
                """{"question":"Which export format is authoritative, CSV or XLSX?"}""")
            .EnqueueText("Parked on the operator's answer.");

        var runner = new PipelineRunner(harness.Services) { NeedsClarificationStatus = "Question" };
        var result = await runner.RunAsync("add-feature");

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("awaiting_user_input");

        var park = tickets.Finalized.Should().ContainSingle().Subject;
        park.Status.Should().Be("Question");
        park.Comment.Should().Contain("Which export format is authoritative, CSV or XLSX?");

        // The run ENDS: a parked add-feature ships nothing — no generated tests, no docs, no PR.
        harness.StubSandboxFactory!.Spawned
            .SelectMany(s => s.Sandbox.RanSteps)
            .Should().NotContain(s => s.Kind == AgentSmith.Sandbox.Wire.StepKind.WriteFile
                && s.Path != null && s.Path.Contains("result.md", StringComparison.Ordinal),
                "a run parked for clarification must not write a run record or open a PR");
    }

    private static RealCompositionHarness BuildHarness(RecordingTicketProvider tickets) =>
        RealCompositionHarness.Build(FixturePaths.For(FixturePaths.Default), services =>
        {
            // The production LLM-driven analyzer would drain the scripted FIFO at AnalyzeCode.
            HarnessProjectAnalyzerStub.Register(services);
            services.RemoveAll<ITicketProviderFactory>();
            services.AddSingleton<ITicketProviderFactory>(new RecordingTicketProviderFactory(tickets));
        });

    private sealed class RecordingTicketProvider : ITicketProvider
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
                ticketId, "Token refresh drops the session",
                "Users are signed out mid-session. Expected: the session survives a refresh.",
                null, "Open", "recording"));

        public Task<CreatedTicket> CreateAsync(
            string title, string description, IReadOnlyList<string> labels,
            CancellationToken cancellationToken) =>
            Task.FromResult(new CreatedTicket(new TicketId("1"), "https://tracker.test/1"));

        public Task<IReadOnlyList<TicketComment>> GetCommentsAsync(
            TicketId ticketId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<TicketComment>>([]);

        public Task FinalizeAsync(
            TicketId ticketId, string comment, string? doneStatus, CancellationToken cancellationToken)
        {
            lock (_finalized) _finalized.Add((ticketId, comment, doneStatus));
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingTicketProviderFactory(RecordingTicketProvider provider)
        : ITicketProviderFactory
    {
        public ITicketProvider Create(TrackerConnection config) => provider;
    }
}
