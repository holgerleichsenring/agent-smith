using AgentSmith.Application.Services.WorkSpecs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Progress;
using AgentSmith.Contracts.WorkSpecs;
using AgentSmith.Domain.Entities;
using AgentSmith.PipelineHarness.Composition;
using FluentAssertions;

namespace AgentSmith.PipelineHarness.Presets;

// p0390: the work spec through the REAL composition root. These prove the three
// rulings the phase is written against — the ticket stays pinned, the ledger seeds
// from the plan and not from the spec, and the verdict pairs with the p0328
// expectation and not with spec criteria — against the actual wired pipeline, not
// against a handler with mocks.
[Trait("Category", "PipelineHarness")]
public sealed class WorkSpecDerivationTests
{
    /// <summary>
    /// The derivation reply every code-preset harness run needs: DeriveSpecification
    /// runs between NegotiateExpectation and GeneratePlan and drains one FIFO slot.
    /// </summary>
    internal const string SpecJson = """
        {"goal": "Reject empty payloads with a 400 instead of a 500",
         "requirements": ["An empty request body is answered with 400.", "Existing callers stay unaffected."],
         "constraints": [{"rule": "No new dependencies.", "sample_anchor": null}],
         "done": [],
         "assumptions": ["The 400 body shape follows the existing error contract."],
         "samples_markdown": "",
         "ignored_instructions": [],
         "handback": {"case": "none", "reason": ""}}
        """;

    [Fact]
    public async Task DeriveSpecification_FixBugRun_PublishesTheCurrentRevisionAndCarriesNoSteps()
    {
        await using var harness = RealCompositionHarness.Build(
            FixturePaths.For(FixturePaths.Default), HarnessProjectAnalyzerStub.Register);
        harness.ChatClient
            .EnqueueText(ExpectationNegotiationTests.DraftJson)
            .EnqueueText(SpecJson)
            .EnqueueText("Planning: I will patch the file.")
            .EnqueueToolCall("write_file", """{"path":"primary/src/Patch.cs","content":"// real fix"}""")
            .EnqueueToolCall("run_command", """{"command":"dotnet build","repo":"primary"}""")
            .EnqueueText(GreenVerdict);

        var runner = new PipelineRunner(harness.Services);
        await runner.RunAsync("fix-bug");

        var artifact = runner.LastContext!.Get<WorkSpecArtifact>(ContextKeys.WorkSpec);
        artifact.Spec.Goal.Should().Contain("Reject empty payloads");
        artifact.Spec.Requirements.Should().HaveCount(2);
        artifact.Spec.Current.Number.Should().Be(1);
        artifact.Spec.Current.Cause.Should().Be(WorkSpecRevisionCause.Initial);
    }

    // p0390 NEGATIVE test: the spec is NOT a gate. The ledger is seeded from the
    // PLAN (p0276), which is what p0341c's precedent demands — a durable structured
    // artifact becomes binding unless something actively stops it.
    [Fact]
    public async Task Ledger_SeedsFromThePlan_NotFromTheSpec()
    {
        await using var harness = RealCompositionHarness.Build(
            FixturePaths.For(FixturePaths.Default), HarnessProjectAnalyzerStub.Register);
        harness.ChatClient
            .EnqueueText(ExpectationNegotiationTests.DraftJson)
            .EnqueueText(SpecJson)
            .EnqueueText(PlanJson)
            .EnqueueToolCall("write_file", """{"path":"primary/src/Patch.cs","content":"// real fix"}""")
            .EnqueueText(GreenVerdict);

        var runner = new PipelineRunner(harness.Services);
        await runner.RunAsync("fix-bug");

        var ledger = runner.LastContext!.Get<ProgressLedger>(ContextKeys.ProgressLedger);
        var spec = runner.LastContext!.Get<WorkSpecArtifact>(ContextKeys.WorkSpec).Spec;
        ledger.Entries.Should().NotBeEmpty();
        ledger.Entries.Select(e => e.Activity).Should().Contain(
            a => a.Contains("boundary", StringComparison.OrdinalIgnoreCase),
            "the ledger is seeded from the plan's steps");
        ledger.Entries.Select(e => e.Activity).Should().NotIntersectWith(
            spec.Requirements, "the spec's requirements must never become ledger items");
    }

    // p0390 NEGATIVE test: the verdict keeps pairing with the p0328 ratified
    // expectation. Two spec requirements and two ratified assertions exist here with
    // DIFFERENT text; the acceptance report must be scored against the expectation's.
    [Fact]
    public async Task Verdict_PairsWithTheExpectation_NotWithSpecCriteria()
    {
        await using var harness = RealCompositionHarness.Build(
            FixturePaths.For(FixturePaths.Default), HarnessProjectAnalyzerStub.Register);
        harness.ChatClient
            .EnqueueText(ExpectationNegotiationTests.DraftJson)
            .EnqueueText(SpecJson)
            .EnqueueText("Planning: I will patch the file.")
            .EnqueueToolCall("write_file", """{"path":"primary/src/Patch.cs","content":"// real fix"}""")
            .EnqueueToolCall("run_command", """{"command":"dotnet build","repo":"primary"}""")
            .EnqueueText(GreenVerdict);

        var runner = new PipelineRunner(harness.Services);
        var result = await runner.RunAsync("fix-bug");

        var spec = runner.LastContext!.Get<WorkSpecArtifact>(ContextKeys.WorkSpec).Spec;
        var expectation = runner.LastContext!
            .Get<Contracts.Expectations.RatifiedExpectation>(ContextKeys.RunExpectation);
        // The spec's done-section carries the ratified assertions VERBATIM and is
        // read-only — one list, and it is the expectation's.
        spec.Done.Should().BeEquivalentTo(expectation.Draft.Expected);
        spec.DoneIsReadOnly.Should().BeTrue();
        result.IsSuccess.Should().BeTrue(
            $"the keystone scored the run against the expectation's two assertions: {result.Message}");
    }

    // p0390 + p0357: the pinned head is what keeps a byte-exact naming contract alive
    // through compaction. The work spec is ADDITIONAL — it must never displace it.
    [Fact]
    public async Task Master_TicketStillPinned_WhenWorkSpecIsPresent()
    {
        await using var harness = RealCompositionHarness.Build(
            FixturePaths.For(FixturePaths.Default), HarnessProjectAnalyzerStub.Register);
        harness.ChatClient
            .EnqueueText(ExpectationNegotiationTests.DraftJson)
            .EnqueueText(SpecJson)
            .EnqueueText("Planning: I will patch the file.")
            .EnqueueToolCall("write_file", """{"path":"primary/src/Patch.cs","content":"// real fix"}""")
            .EnqueueText(GreenVerdict);

        var runner = new PipelineRunner(harness.Services);
        await runner.RunAsync("fix-bug");

        var ticket = runner.LastContext!.Get<Ticket>(ContextKeys.Ticket);
        var userText = harness.ChatClient.LastMessages
            .Single(m => m.Role == Microsoft.Extensions.AI.ChatRole.User).Text;

        runner.LastContext!.Has(ContextKeys.WorkSpec).Should().BeTrue(
            "the run derived a spec, so this asserts the pinning WITH one present");
        userText.Should().Contain(ticket.Description,
            "the ticket stays PINNED verbatim in the first user message alongside the spec (p0357)");
        userText.Should().Contain(ticket.Title,
            "the whole ticket is pinned, not a reduction of it");
    }

    private const string GreenVerdict =
        """Done. {"status":"green","build_ran":true,"build_passed":true,"tests_ran":true,"tests_passed":true,"summary":"fixed","acceptance":[{"criterion":"criterion 1","status":"met","evidence":"handled in the change"},{"criterion":"criterion 2","status":"met","evidence":"existing behaviour preserved"}]}""";

    private const string PlanJson = """
        {"summary": "fix the boundary comparison",
         "steps": [{"order": 1, "description": "correct the boundary comparison", "change_type": "modify", "target_file": "src/Patch.cs"}]}
        """;
}
