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
/// p0315d fast-tier coverage for the spec-driven scenario, through the REAL composition:
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
public sealed class SpecExecutionTests
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

    private const string SecondYaml =
        """
        spec: 2026-10-06-5e5eb
        goal: "Store posted widgets in the sample service"
        steps:
          - id: store
            action: "Add the widget store"
        done:
          - "POST /widget stores the widget"
        """;

    [Fact]
    public async Task SpecExecution_RunsStepsThenVerifiesDoneCriteria()
    {
        // The p0317 comment thread: an operator answer posted while the ticket
        // was parked for clarification. FetchTicket hydrates it on the
        // re-triggered run, so the answer reaches the master even though the
        // comment-re-trigger path was status-gated out at post time (the
        // p0315d parked-while-answered residual, closed by this merge).
        var tickets = new SpecTicketProvider(SpecTicketBody(),
            comments:
            [
                new TicketComment(
                    "operator", new DateTimeOffset(2026, 7, 1, 9, 0, 0, TimeSpan.Zero),
                    "Answer to your question: use bearer-token auth for the widget endpoint"),
                // The cut comment the set's own publish posted after it: the answer is not input
                // the set has not seen, so the seeded spec is worked unamended.
                new TicketComment(
                    "agent-smith", new DateTimeOffset(2026, 7, 1, 10, 0, 0, TimeSpan.Zero),
                    AgentSmith.Application.Services.Specs.SpecSetComment.Render(SeededSet(), null, AgentSmith.Tests.TestSupport.SeriesSettingsDoubles.Defaults)),
            ]);
        await using var harness = BuildHarness(tickets);
        harness.ChatClient
            // p0394a: no plan slot — the spec IS the plan; the master consumes first.
            .EnqueueToolCall("write_file", """{"path":"primary/src/Widget.cs","content":"// widget endpoint"}""")
            .EnqueueToolCall("run_command", """{"command":"dotnet test","repo":"primary"}""")
            .EnqueueToolCall("update_progress", """{"items":[{"id":"impl","activity":"Add the widget endpoint + handler","status":"done"}]}""")
            .EnqueueFinish(summary: "widget endpoint shipped");

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
            "the hydrated comment thread must render into the spec prompt");
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
            "the spec yaml must be written to .agentsmith/specs/done/ in the sandbox tree");

        harness.ChatClient.ToolCalls.ShouldHaveCalledInOrder("write_file", "run_command", "update_progress");

    }

    [Fact]
    public async Task SpecExecution_MasterNeedsInput_MovesTicketToNeedsClarification()
    {
        var tickets = new SpecTicketProvider(SpecTicketBody());
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
                "a run parked for clarification must not record the spec as done");
    }

    /// <summary>2026-10-06-03c7e: a run whose second spec fails verification leaves the first in
    /// done/ with its outcome, the second in planned/, and one index line — the first's.</summary>
    [Fact]
    public async Task SpecExecution_SecondSpecFailsVerification_RecordsOnlyTheFirst()
    {
        await using var harness = BuildHarness(new SpecTicketProvider(SpecTicketBody()), TwoSpecSet());
        RedForTheSecondSpec(harness);
        ScriptSpec(harness, "primary/src/Widget.cs", "impl");
        ScriptSpec(harness, "primary/src/WidgetStore.cs", "store");
        harness.ChatClient.EnqueueFinish();

        var runner = new PipelineRunner(harness.Services);
        await runner.RunAsync("code");

        var steps = Steps(harness);
        steps.Should().Contain(s => IsWrite(s, $"{SeriesPaths.Done}/{First}-") && s.Content!.Contains("outcome:"),
            "the executed spec is the spec plus its outcome, in done/");
        steps.Should().NotContain(s => IsWrite(s, $"{SeriesPaths.Done}/{Second}-"), "b did not pass, so it stays planned");
        Removed(steps).Should().Contain(p => p.StartsWith($"{SeriesPaths.Planned}/{First}-", StringComparison.Ordinal))
            .And.NotContain(p => p.Contains(Second, StringComparison.Ordinal));
        steps.Count(s => IsWrite(s, ".agentsmith/contexts/") && s.Content!.Contains(First, StringComparison.Ordinal))
            .Should().Be(1, "one index line, written once, in the carrying repository");
        steps.Should().NotContain(s => IsWrite(s, ".agentsmith/contexts/") && s.Content!.Contains($"{Second}:", StringComparison.Ordinal));
        runner.LastContext!.Get<SpecSequenceProgress>(ContextKeys.SpecSequenceProgress).Specs
            .Select(p => p.State).Should().Equal(SpecRunState.Done, SpecRunState.Failed);
    }

    /// <summary>2026-10-06-03c7e: re-triggered after that, the branch reads a as executed (it lies
    /// in done/), so the run continues with b and never re-runs a.</summary>
    [Fact]
    public async Task SpecExecution_ReTriggerAfterFirstSpecExecuted_ContinuesWithTheSecond()
    {
        await using var harness = BuildHarness(
            new SpecTicketProvider(SpecTicketBody()), TwoSpecSet() with { Executed = [First] });
        ScriptSpec(harness, "primary/src/WidgetStore.cs", "store");

        var runner = new PipelineRunner(harness.Services);
        var result = await runner.RunAsync("code");

        result.IsSuccess.Should().BeTrue(result.Message);
        harness.ChatClient.ToolCalls.Count(c => c.Name == "write_file").Should().Be(1, "only b is worked");
        var steps = Steps(harness);
        steps.Should().NotContain(s => IsWrite(s, $"{SeriesPaths.Done}/{First}-"), "a's record is already on the branch");
        steps.Should().Contain(s => IsWrite(s, $"{SeriesPaths.Done}/{Second}-"));
        runner.LastContext!.Get<SpecSequenceProgress>(ContextKeys.SpecSequenceProgress).Specs
            .Select(p => p.State).Should().Equal(SpecRunState.Done, SpecRunState.Done);
    }

    /// <summary>
    /// 2026-11-18-c8e2d: the lead loop end to end. Spec a's master starts a builder in the background, the builder leaves
    /// the test out, the master sends the work back and the builder adds it. Spec b's first build is red; the red output
    /// goes back to its master, which sends it to its builder, which fixes it; the master leaves one done line as a
    /// follow-up instead of holding the spec. Both specs deliver and the run's report carries the follow-up.
    /// </summary>
    [Fact]
    public async Task SpecExecution_MasterLeadsChildren_SendsWorkBack_ResolvesARedBuild_NamesAFollowUp()
    {
        await using var harness = BuildHarness(new SpecTicketProvider(SpecTicketBody()), LeadSpecSet());
        harness.StubSandboxFactory!
            .RedAfterWriting("WidgetStore.cs", "dotnet build", "error CS1002: ; expected")
            .AnswerAfterWriting("WidgetStoreFix.cs", "dotnet build", null);
        var chat = harness.ChatClient;
        // Spec a: start, wait, send back, wait, report.
        chat.EnqueueToolCall("spawn_agents", Spawn("WidgetBuilder", "build the widget endpoint"))
            .EnqueueToolCall("send_message", """{"wait":true}""")
            .EnqueueToolCall("send_message", """{"agent_id":"WidgetBuilder","text":"The done list asks for the 404 to be tested; add that test."}""")
            .EnqueueToolCall("send_message", """{"wait":true}""")
            .EnqueueToolCall("update_progress", """{"items":[{"id":"impl","activity":"the work","status":"done"}]}""")
            .EnqueueFinish(summary: "the widget endpoint", report: "## Delivered\n- GET /widget returns the widget (`src/Widget.cs`), its 404 tested");
        chat.EnqueueForToolCall("WidgetBuilder", "write_file", """{"path":"primary/src/Widget.cs","content":"// endpoint"}""")
            .EnqueueForText("WidgetBuilder", "Built the endpoint in src/Widget.cs. No test yet.")
            .EnqueueForToolCall("WidgetBuilder", "write_file", """{"path":"primary/tests/WidgetTests.cs","content":"// 404 test"}""")
            .EnqueueForText("WidgetBuilder", "Added tests/WidgetTests.cs covering the 404.");
        // Spec b: start, wait, report with a follow-up; then the stage repair: send the red output back, wait, report again.
        chat.EnqueueToolCall("spawn_agents", Spawn("StoreBuilder", "build the widget store"))
            .EnqueueToolCall("send_message", """{"wait":true}""")
            .EnqueueToolCall("update_progress", """{"items":[{"id":"store","activity":"the work","status":"done"}]}""")
            .EnqueueFinish(summary: "the widget store",
                report: "## Delivered\n- POST /widget stores the widget (`src/WidgetStore.cs`)\n\n## Open\n- CSV export needs a paging contract first",
                followUps: [("Stored widgets are exported as CSV over paging", "the paging contract is its own design")])
            .EnqueueToolCall("send_message", """{"agent_id":"StoreBuilder","text":"The build is red: error CS1002. Fix it."}""")
            .EnqueueToolCall("send_message", """{"wait":true}""")
            .EnqueueFinish(summary: "the widget store, build fixed", report: "## Fixed\n- the missing semicolon (`src/WidgetStoreFix.cs`); the build is green");
        chat.EnqueueForToolCall("StoreBuilder", "write_file", """{"path":"primary/src/WidgetStore.cs","content":"// store"}""")
            .EnqueueForText("StoreBuilder", "Built the store in src/WidgetStore.cs.")
            .EnqueueForToolCall("StoreBuilder", "write_file", """{"path":"primary/src/WidgetStoreFix.cs","content":"// fixed"}""")
            .EnqueueForText("StoreBuilder", "Fixed the missing semicolon.");

        var runner = new PipelineRunner(harness.Services);
        var result = await runner.RunAsync("code");

        result.IsSuccess.Should().BeTrue(result.Message);
        var builder = chat.AgentCalls("WidgetBuilder").Last();
        builder.Should().Contain(m => m.Role == Microsoft.Extensions.AI.ChatRole.User && m.Text.Contains("add that test"),
            "the master's feedback is the builder's next turn");
        builder.Should().Contain(m => m.Text.Contains("No test yet"), "over its own earlier conversation");
        chat.AgentCalls("StoreBuilder").Last().Should().Contain(m => m.Text.Contains("The build is red"));
        var accounts = AgentSmith.Application.Services.Specs.RunAccountLedger.Current(runner.LastContext!);
        accounts.All.SelectMany(a => a.FollowUps ?? []).Should().ContainSingle().Which.Goal.Should().Be("Stored widgets are exported as CSV over paging");
        // 2026-11-18-98fa: the account is the master's markdown — the repair's report stands, nothing is judged per line.
        var second = accounts.Specs.Single(s => s.SpecId == LeadSecond).Accounts.Single();
        second.Criteria.Should().BeEmpty();
        second.Report.Should().StartWith("## Fixed");
        accounts.Specs.Single(s => s.SpecId != LeadSecond).Accounts.Single().Report.Should().Contain("its 404 tested");
    }

    private const string LeadSecond = "2026-10-06-5e5ec";

    private static string Spawn(string name, string task) => $$$"""
        {"background":true,"tasks":[{"name":"{{{name}}}","activity":"{{{task}}}","task_description":"{{{task}}}; report what you changed",
          "inherited_context":{"pipeline_goal":"the widget service","prior_context_slice":"the spec and the code read so far"}}]}
        """;

    private static SpecSet LeadSpecSet() => SeededSet() with
    {
        Specs =
        [
            SeededSet().Specs[0],
            new DerivedSpec(new SpecDraftReader().Read("""
                spec: 2026-10-06-5e5ec
                goal: "Store posted widgets in the sample service"
                steps:
                  - id: store
                    action: "Add the widget store"
                done:
                  - "POST /widget stores the widget"
                  - "Stored widgets are exported as CSV"
                """), "store-posted-widgets", string.Empty, []),
        ],
    };

    private const string First = "2026-10-06-5e5ea";
    private const string Second = "2026-10-06-5e5eb";
    private const string SecondCriterion = "POST /widget stores the widget";

    private static void ScriptSpec(RealCompositionHarness harness, string path, string step) =>
        harness.ChatClient
            .EnqueueToolCall("write_file", $$"""{"path":"{{path}}","content":"// work"}""")
            .EnqueueToolCall("update_progress", $$"""{"items":[{"id":"{{step}}","activity":"the work","status":"done"}]}""")
            .EnqueueFinish(summary: "done");

    // 2026-11-18-c8e2c: b fails on a red stage — red at its verification and again after its one repair pass.
    private static void RedForTheSecondSpec(RealCompositionHarness harness) =>
        harness.StubSandboxFactory!.RedAfterWriting("WidgetStore.cs", "dotnet build", "error CS1002: ; expected");

    private static List<AgentSmith.Sandbox.Wire.Step> Steps(RealCompositionHarness harness) =>
        [.. harness.StubSandboxFactory!.Spawned.SelectMany(s => s.Sandbox.RanSteps)];

    private static bool IsWrite(AgentSmith.Sandbox.Wire.Step step, string pathPart) =>
        step.Kind == AgentSmith.Sandbox.Wire.StepKind.WriteFile
        && step.Path is { } p && p.Contains(pathPart, StringComparison.Ordinal);

    private static IReadOnlyList<string> Removed(IEnumerable<AgentSmith.Sandbox.Wire.Step> steps) =>
        [.. steps.Where(s => s.Command == "git" && s.Args is ["rm", ..]).SelectMany(s => s.Args!)];

    private static RealCompositionHarness BuildHarness(SpecTicketProvider tickets, SpecSet? seeded = null) =>
        RealCompositionHarness.Build(FixturePaths.For(FixturePaths.Default), services =>
        {
            services.RemoveAll<ISpecSetReader>();
            services.AddSingleton<ISpecSetReader>(new SeededSpecSetReader(seeded ?? SeededSet(), "seeded-sha"));
            // The analyzer would drain the scripted FIFO at AnalyzeCode (same
            // reason FixBug's keystone tests stub it).
            HarnessProjectAnalyzerStub.Register(services);
            // The ticket tracker HTTP boundary: a recording provider that serves
            // the spec ticket and captures the park call.
            services.RemoveAll<ITicketProviderFactory>();
            services.AddSingleton<ITicketProviderFactory>(new SpecTicketProviderFactory(tickets));
        });

    private static string SpecTicketBody() => "## Goal\nAdd a widget endpoint to the sample service\n";

    // The spec as the branch carries it: one planned spec of a series, no pointer recorded, so
    // the run works it unamended.
    private static SpecSet SeededSet() => new(
        TicketKey.For("recording", "1").Value,
        [new DerivedSpec(new SpecDraftReader().Read(ValidYaml), "add-a-widget-endpoint-to-the-sample-service",
            string.Empty, [])],
        SpecAccounting.Empty,
        [new SpecRevision(1, "initial derivation", DateTimeOffset.UtcNow.AddHours(-1))],
        SpecSource.BranchArtifact,
        Series: "2026-10-06-5e5e");

    private static SpecSet TwoSpecSet() => SeededSet() with
    {
        Specs =
        [
            SeededSet().Specs[0],
            new DerivedSpec(new SpecDraftReader().Read(SecondYaml), "store-posted-widgets", string.Empty, []),
        ],
    };

    private sealed class SeededSpecSetReader(SpecSet set, string sha) : ISpecSetReader
    {
        public Task<SpecSetOnBranch> ReadAsync(
            PipelineContext pipeline, RepoConnection carryingRepo, IReadOnlyList<RepoConnection> repositories,
            TicketKey ticket, CancellationToken cancellationToken) =>
            Task.FromResult(SpecSetOnBranch.Answered(new SpecSetReadResult(set, sha)));
    }

    private sealed class SpecTicketProvider(
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

    private sealed class SpecTicketProviderFactory(SpecTicketProvider provider) : ITicketProviderFactory
    {
        public ITicketProvider Create(TrackerConnection config) => provider;

        public ITicketRewriter CreateRewriter(TrackerConnection config) => new HarnessTicketRewriter();

        public ITicketSearch CreateSearch(TrackerConnection config) => new HarnessTicketSearch();

        public ITicketLinkedWork CreateLinkedWork(TrackerConnection config) =>
            new RecordingLinkedWork();
    }
}
