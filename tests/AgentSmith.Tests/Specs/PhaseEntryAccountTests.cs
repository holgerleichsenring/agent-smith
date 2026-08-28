using AgentSmith.Application.Services.Events;
using AgentSmith.Application.Models;
using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Models;
using AgentSmith.Sandbox.Wire;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// p0460: a phase whose ratified criteria the branch already satisfies is already done.
/// <para>
/// A run parks or dies mid-sequence with its work committed, the operator re-triggers, and
/// the new run opens phase a as if nothing had happened — a master pass, real money and
/// often an operator question spent on work the branch already carries. The answer is
/// always the same, so it is taken rather than asked.
/// </para>
/// </summary>
public sealed class PhaseEntryAccountTests
{
    private const string Criterion = "the handler is migrated";
    private const string SourceDiff =
        "diff --git a/src/Handler.cs b/src/Handler.cs\n--- a/src/Handler.cs\n"
        + "+++ b/src/Handler.cs\n@@ -1 +1 @@\n+migrated\n";
    private const string RecordOnlyDiff =
        "diff --git a/.agentsmith/runs/r1/plan.md b/.agentsmith/runs/r1/plan.md\n"
        + "--- a/.agentsmith/runs/r1/plan.md\n+++ b/.agentsmith/runs/r1/plan.md\n@@ -1 +1 @@\n+planned\n";

    [Fact]
    public async Task APhaseWhoseCriteriaTheBranchAlreadySatisfies_IsNotWorkedAgain()
    {
        var accountant = new CountingAccountant(AccountDisposition.Satisfied);
        var pipeline = Pipeline(SourceDiff);

        var result = await Select(accountant, pipeline);

        result.IsSuccess.Should().BeTrue();
        result.DropAhead.Should().BeEquivalentTo(PipelinePresets.PhaseWorkSteps,
            "the master pass, its questions, the commit and the gate all have nothing to do");
        result.DropAhead.Should().NotContain(CommandNames.WritePhaseRecord,
            "the record is how the branch says this phase is through");
    }

    /// <summary>
    /// 2026-08-25-9749: the entry account is the second of the two bool readers that live
    /// outside the account's own file, and the easiest to miss. A phase whose every criterion
    /// the base declares not applicable has PROVEN nothing — skipping it here would record it
    /// as through on the strength of an answer about what was never there.
    /// </summary>
    [Fact]
    public async Task SelectPhase_AnEntryAccountOfOnlyNotApplicable_DoesNotSkipThePhase()
    {
        var accountant = new CountingAccountant(AccountDisposition.NotApplicable);
        var pipeline = Pipeline(SourceDiff);

        var result = await Select(accountant, pipeline);

        result.IsSuccess.Should().BeTrue();
        result.DropAhead.Should().BeNull("the phase still has to be worked");
    }

    /// <summary>A skipped phase that leaves no trace is indistinguishable from one that
    /// never ran — and the run would be reporting silence as delivery.</summary>
    [Fact]
    public async Task ASkippedPhase_SaysItWasSatisfiedAndByWhat()
    {
        var pipeline = Pipeline(SourceDiff);

        var result = await Select(new CountingAccountant(AccountDisposition.Satisfied), pipeline);

        result.Message.Should().Contain("already satisfied by the branch");
        result.Message.Should().Contain("src/Handler.cs", "an operator must see WHAT satisfied it");
        var progress = pipeline.Get<SpecSequenceProgress>(ContextKeys.SpecSequenceProgress);
        progress.Phases.Single(p => p.PhaseId == "p1").State.Should().Be(PhaseRunState.Done);
        progress.Phases.Single(p => p.PhaseId == "p1").Note
            .Should().Be(SelectPhaseHandler.AlreadySatisfiedNote);
        pipeline.Get<IReadOnlyList<SpecAccount>>(ContextKeys.PhaseAccounts).Should().ContainSingle(
            "the phase record renders the account it was skipped on");
        RunAccountLedger.Current(pipeline).All.Should().NotBeEmpty(
            "the run's own delivery gate is judged on these accounts too");
    }

    [Fact]
    public async Task APartlySatisfiedPhase_StillRuns()
    {
        var pipeline = Pipeline(SourceDiff);

        var result = await Select(new CountingAccountant(AccountDisposition.Satisfied, outstanding: "the tests pass"), pipeline);

        result.DropAhead.Should().BeNull("one outstanding criterion is a phase with work left");
        pipeline.Get<SpecSequenceProgress>(ContextKeys.SpecSequenceProgress)
            .Phases.Single(p => p.PhaseId == "p1").State.Should().Be(PhaseRunState.InProgress);
    }

    [Fact]
    public async Task AnAccountThatCouldNotBeTaken_DoesNotCountAsSatisfied()
    {
        var pipeline = Pipeline(SourceDiff);

        var result = await Select(new ThrowingAccountant(), pipeline);

        result.DropAhead.Should().BeNull("an account that could not be taken is not a pass");
    }

    /// <summary>
    /// The fresh-branch case, which is most runs: an account over an empty diff can only
    /// say "nothing is satisfied", so it is never paid for.
    /// </summary>
    [Fact]
    public async Task ABranchThatCarriesNothing_IsNeverAccountedForAtEntry()
    {
        var accountant = new CountingAccountant(AccountDisposition.Satisfied);

        var result = await Select(accountant, Pipeline(string.Empty));

        accountant.Calls.Should().Be(0, "there is no model call to pay for on an empty branch");
        result.DropAhead.Should().BeNull();
    }

    [Fact]
    public async Task ABranchCarryingOnlyTheRunsOwnRecord_IsNoDelivery()
    {
        var accountant = new CountingAccountant(AccountDisposition.Satisfied);

        await Select(accountant, Pipeline(RecordOnlyDiff));

        accountant.Calls.Should().Be(0, ".agentsmith bookkeeping satisfies no criterion");
    }

    [Fact]
    public async Task ADiffThatCouldNotBeTaken_LeavesThePhaseToBeWorked()
    {
        var accountant = new CountingAccountant(AccountDisposition.Satisfied);

        var result = await Select(accountant, Pipeline(SourceDiff, diffExitCode: 1));

        accountant.Calls.Should().Be(0, "unknown is not empty and not satisfied");
        result.DropAhead.Should().BeNull();
    }

    [Fact]
    public async Task APhaseWithoutRatifiedCriteria_IsNeverSkipped()
    {
        var accountant = new CountingAccountant(AccountDisposition.Satisfied);
        var pipeline = Pipeline(SourceDiff, done: []);

        var result = await Select(accountant, pipeline);

        accountant.Calls.Should().Be(0, "nothing was ratified, so nothing can be satisfied");
        result.DropAhead.Should().BeNull();
    }

    private static Task<CommandResult> Select(ISpecAccountant accountant, PipelineContext pipeline) =>
        new SelectPhaseHandler(
                Entry(accountant), new PhaseProgressRecorder(new NoOpEventPublisher()),
                NullLogger<SelectPhaseHandler>.Instance)
            .ExecuteAsync(new SelectPhaseContext("p1", pipeline), CancellationToken.None);

    private static PhaseEntryAccount Entry(ISpecAccountant accountant) =>
        new(new DeliveryDiff(AgentSmith.Tests.TestHelpers.TestGit.BaseBranch, NullLogger<DeliveryDiff>.Instance),
            new PhaseAccounting(
                new DeliveryDiff(AgentSmith.Tests.TestHelpers.TestGit.BaseBranch, NullLogger<DeliveryDiff>.Instance), accountant,
                new SandboxTargets(), NullLogger<PhaseAccounting>.Instance),
            new SandboxTargets(), NullLogger<PhaseEntryAccount>.Instance);

    private static PipelineContext Pipeline(
        string diff, int diffExitCode = 0, IReadOnlyList<string>? done = null)
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.ResolvedPipeline,
            new ResolvedPipelineConfig("code", new AgentConfig(), "skills", null));
        pipeline.Set(ContextKeys.SpecSet, Set(done ?? [Criterion]));
        pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(
            ContextKeys.Sandboxes,
            new Dictionary<string, ISandbox> { ["server"] = new DiffSandbox(diff, diffExitCode) });
        pipeline.Set<IReadOnlyDictionary<string, RemoteContextDiscovery>>(
            ContextKeys.SandboxDiscoveries,
            new Dictionary<string, RemoteContextDiscovery> { ["server"] = new("default", ".", "csharp") });
        return pipeline;
    }

    private static SpecSet Set(IReadOnlyList<string> done) => new(
        "azdo-1",
        [new SpecPhase(
            new PhaseDraft("p1", "Migrate the handler", "phase: p1", []) { Done = done },
            "p1", string.Empty, [])],
        SpecAccounting.Empty,
        [new SpecRevision(1, "initial derivation", DateTimeOffset.UtcNow)],
        SpecSource.Derived);

    /// <summary>Answers `git diff` with the branch this case is about; everything else
    /// succeeds silently, as a checked-out sandbox does.</summary>
    private sealed class DiffSandbox(string diff, int diffExitCode) : ISandbox
    {
        public string JobId => "diff-sandbox";

        public Task<StepResult> RunStepAsync(
            Step step, IProgress<StepEvent>? progress, CancellationToken cancellationToken)
        {
            var isDiff = step.Command == "git" && step.Args is { } a && a.Contains("diff");
            return Task.FromResult(new StepResult(
                StepResult.CurrentSchemaVersion, step.StepId,
                ExitCode: isDiff ? diffExitCode : 0, TimedOut: false, DurationSeconds: 0,
                ErrorMessage: null, OutputContent: isDiff ? diff : string.Empty));
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class CountingAccountant(
        AccountDisposition disposition, string? outstanding = null) : ISpecAccountant
    {
        public int Calls { get; private set; }

        public Task<SpecAccount> AccountAsync(
            string repoKey, IReadOnlyList<string> criteria, string diff,
            IReadOnlyList<string> commandResults, AgentConfig agent,
            BranchSearch? branchSearch,
            PipelineCostTracker costTracker, CancellationToken cancellationToken, int windowBudgetChars)
        {
            Calls++;
            var rows = criteria
                .Select(c => new CriterionAccount(
                    c, disposition, "src/Handler.cs",
                    Antecedent: disposition is AccountDisposition.NotApplicable
                        ? "a previously configured transport"
                        : null))
                .Append(outstanding is null
                    ? null
                    : new CriterionAccount(outstanding, AccountDisposition.NotSatisfied, null, "not on the branch"))
                .OfType<CriterionAccount>()
                .ToList();
            return Task.FromResult(new SpecAccount(repoKey, rows));
        }
    }

    private sealed class ThrowingAccountant : ISpecAccountant
    {
        public Task<SpecAccount> AccountAsync(
            string repoKey, IReadOnlyList<string> criteria, string diff,
            IReadOnlyList<string> commandResults, AgentConfig agent,
            BranchSearch? branchSearch,
            PipelineCostTracker costTracker, CancellationToken cancellationToken, int windowBudgetChars) =>
            throw new InvalidOperationException("429 Too Many Requests");
    }
}
