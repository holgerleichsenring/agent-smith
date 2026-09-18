using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Resume;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Models;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// 2026-09-17-042eh: the ONE pass a review's findings earn — what it splices, what it must
/// snapshot first, and what a pass that is not green leaves behind.
/// </summary>
public sealed class PhaseReviewFixPassTests
{
    private const string Key = "api";
    private const string PhaseId = "2026-09-17-042eh";

    [Fact]
    public void PhaseReview_WithFindings_SplicesOneFixPassEndingInReview()
    {
        var pipeline = Verified();

        var decision = PhaseReviewFixPass.Decide(pipeline, Found(), Sandboxes(new StubSandbox()));

        decision.Steps!.Select(c => c.Name).Should().Equal(
            CommandNames.AgenticMaster, CommandNames.MasterOpenQuestions,
            CommandNames.CommitPhaseWork, CommandNames.VerifyPhase, CommandNames.ReviewPhaseDiff);
        decision.Steps.Should().OnlyContain(c => c.PhaseId == PhaseId,
            "a repeated step belongs to the phase it repeats");
    }

    [Fact]
    public void PhaseReview_SandboxWithoutAVerifiedHead_RecordsFindingsWithoutAFixPass()
    {
        var pipeline = Verified();
        pipeline.Set<IReadOnlyDictionary<string, string>>(
            ContextKeys.VerifiedHeads, new Dictionary<string, string>(StringComparer.Ordinal));

        var decision = PhaseReviewFixPass.Decide(pipeline, Found(), Sandboxes(new StubSandbox()));

        decision.Steps.Should().BeNull("a pass that could not be reverted must not be spliced");
        decision.Note.Should().Be($"no fix pass: {Key} has no verified head");
    }

    [Fact]
    public void PhaseReview_SecondReview_SplicesNothing()
    {
        var pipeline = Verified();
        PhaseReviewFixPass.Prepare(pipeline);

        PhaseReviewFixPass.Decide(pipeline, Found(), Sandboxes(new StubSandbox())).Steps
            .Should().BeNull("one pass, not a carousel");
    }

    [Fact]
    public void FixPass_AfterARepairPass_PromptCarriesNoOutstandingCriteria()
    {
        var pipeline = Verified();
        pipeline.Set(ContextKeys.OutstandingCriteria, new List<string> { "api: the old name is gone" });

        PhaseReviewFixPass.Prepare(pipeline);

        PhaseExecutionPromptBlocks.OutstandingCriteria(pipeline).Should().BeEmpty(
            "a REPAIR instruction standing beside the findings tells the master to close a "
            + "different list and call the rest accounted for");
    }

    [Fact]
    public void PhaseReview_FixPassPrepare_SnapshotsTheAccountAndSpendsTheRepair()
    {
        var pipeline = Verified();
        pipeline.Set<IReadOnlyList<SpecAccount>>(ContextKeys.PhaseAccounts, [Account("first")]);
        RunAccountLedger.Record(pipeline, [Account("first")]);

        PhaseReviewFixPass.Prepare(pipeline);
        pipeline.Set<IReadOnlyList<SpecAccount>>(ContextKeys.PhaseAccounts, [Account("the fix pass")]);
        RunAccountLedger.Record(pipeline, [Account("the fix pass")]);
        PhaseReviewFixPass.RestoreSnapshot(pipeline);

        pipeline.Get<IReadOnlyList<SpecAccount>>(ContextKeys.PhaseAccounts)[0].Problem.Should().Be("first");
        RunAccountLedger.Current(pipeline).All[0].Problem.Should().Be("first");
        pipeline.Get<bool>(ContextKeys.PhaseRepairAttempted).Should().BeTrue(
            "the verification inside the pass must return its verdict, not splice a repair of its own");
    }

    [Fact]
    public async Task PhaseReview_RevertedFixPass_RunDeliveryGateStaysGreen()
    {
        var pipeline = InTheFixPass(new StubSandbox(), Delivered());
        // What the fix pass's own verification would have written before it failed.
        RunAccountLedger.Record(pipeline, [Account("a criterion the fix made outstanding")]);

        var result = await TestPhaseReview.Revert().TryRevertAsync(
            pipeline, CommandResult.Fail("Verification failed: api build exited 1"),
            CancellationToken.None);

        result!.IsSuccess.Should().BeTrue("a verified phase is not failed by a fix that was not kept");
        RunDeliveryGate.Evaluate(RunAccountLedger.Current(pipeline), 1).Satisfied.Should().BeTrue();
        PhaseReviewLedger.ForThisPhase(pipeline).Findings.Should().OnlyContain(
            f => f.Reverted!.StartsWith("fix pass reverted", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PhaseReview_FixPassWithAnOutstandingCriterion_RevertsAndSplicesNoMasterPass()
    {
        var pipeline = InTheFixPass(new StubSandbox(), Delivered());

        // A GREEN build whose account now has a criterion outstanding is still not success,
        // and is exactly the case a build-only reading of "red" would have let through.
        var result = await TestPhaseReview.Revert().TryRevertAsync(
            pipeline,
            CommandResult.Fail("1 criterion(s) of the ratified phase are not satisfied by the branch"),
            CancellationToken.None);

        result!.IsSuccess.Should().BeTrue();
        result.InsertNext.Should().BeNull("a revert is the end of the pass, not another one");
        pipeline.Get<string>(ContextKeys.PhaseReviewReverted).Should().StartWith("fix pass reverted");
    }

    [Fact]
    public async Task PhaseReview_RevertWithNothingStagedInARepository_CommitsNothingAndStandsDone()
    {
        var sandbox = new StubSandbox();
        var pipeline = InTheFixPass(sandbox);

        var result = await TestPhaseReview.Revert().TryRevertAsync(
            pipeline, CommandResult.Fail("Verification failed"), CancellationToken.None);

        result!.IsSuccess.Should().BeTrue();
        sandbox.RanSteps.Should().NotContain(
            step => step.Command == "git" && step.Args!.Contains("commit"),
            "the push primitive commits nothing when nothing is staged; the bare commit throws");
    }

    [Fact]
    public async Task PhaseReview_SandboxThatCannotBeRestored_FailsThePhaseNamingIt()
    {
        // The checkout that brings the tree back fails; everything else succeeds, so this is
        // the revert's own failure and not a missing precondition.
        var sandbox = new ScriptedGitSandbox(new Dictionary<string, (int, string)>(StringComparer.Ordinal)
        {
            ["--name-status"] = (0, "M\tsrc/Api/Handler.cs\n"),
            ["checkout"] = (1, string.Empty),
        });
        var pipeline = InTheFixPass(sandbox);

        var result = await TestPhaseReview.Revert().TryRevertAsync(
            pipeline, CommandResult.Fail("Verification failed: api build exited 1"),
            CancellationToken.None);

        result!.IsSuccess.Should().BeFalse(
            "a tree carrying unverified work must never be recorded done");
        result.Message.Should().Contain(Key).And.Contain("could not be brought back");
        result.Message.Should().Contain("api build exited 1", "the reader needs both failures");
    }

    [Fact]
    public async Task PhaseReview_RevertWithNoSandboxResolvable_FailsRatherThanReportingSuccess()
    {
        var pipeline = Verified();
        PhaseReviewFixPass.Prepare(pipeline);

        var result = await TestPhaseReview.Revert().TryRevertAsync(
            pipeline, CommandResult.Fail("Verification failed"), CancellationToken.None);

        result!.IsSuccess.Should().BeFalse(
            "a fix pass ran in SOME tree; 'reverted' over a map that no longer resolves would "
            + "record the phase done over work nobody brought back");
        result.Message.Should().Contain("no sandbox could be resolved");
    }

    [Fact]
    public async Task PhaseReview_RestoredButNotCommitted_FailsThePhase()
    {
        // The index comes back clean, but the branch still carries the red fix: the push
        // declined (a secret-pattern match, a repo with no sandbox) and reported nothing.
        var sandbox = new ScriptedGitSandbox(new Dictionary<string, (int, string)>(StringComparer.Ordinal)
        {
            ["--name-status"] = (0, string.Empty),
            ["--name-only"] = (0, "src/Api/Handler.cs\n"),
        });
        var pipeline = InTheFixPass(sandbox);

        var result = await TestPhaseReview.Revert().TryRevertAsync(
            pipeline, CommandResult.Fail("Verification failed"), CancellationToken.None);

        result!.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("the restored state was not committed");
    }

    [Fact]
    public async Task PhaseReview_RevertOutsideAFixPass_IsNotThisCollaboratorsBusiness()
    {
        var reverted = await TestPhaseReview.Revert().TryRevertAsync(
            Verified(), CommandResult.Fail("Verification failed"), CancellationToken.None);

        reverted.Should().BeNull("an ordinary red verification is still a verdict on the phase");
    }

    [Fact]
    public void PhaseReview_NextPhase_StartsWithNoFindingsCarriedOver()
    {
        var pipeline = Verified();
        PhaseReviewLedger.Record(pipeline, Found());
        PhaseReviewFixPass.Prepare(pipeline);

        PhaseRepairScope.Reset(pipeline);

        PhaseReviewLedger.ForThisPhase(pipeline).Findings.Should().BeEmpty();
        PhaseReviewFixPass.InFlight(pipeline).Should().BeFalse();
        pipeline.TryGet<IReadOnlyList<SpecAccount>>(ContextKeys.PhaseAccountsSnapshot, out _)
            .Should().BeFalse();
        PhaseReviewLedger.Current(pipeline).AllFindings.Should().ContainSingle(
            "the RUN ledger is what the pull request reads — only the per-phase key clears");
    }

    [Fact]
    public void FixPass_ParkedAndResumed_KeepsSnapshotAndFlags()
    {
        var pipeline = Verified();
        pipeline.Set<IReadOnlyList<SpecAccount>>(ContextKeys.PhaseAccounts, [Account("first")]);
        RunAccountLedger.Record(pipeline, [Account("first")]);
        PhaseReviewLedger.Record(pipeline, Found());
        PhaseReviewFixPass.Prepare(pipeline);

        var serializer = new PipelineContextSerializer(
            NullLogger<PipelineContextSerializer>.Instance);
        var resumed = new PipelineContext();
        serializer.Restore(serializer.Serialize(pipeline), resumed);

        PhaseReviewFixPass.InFlight(resumed).Should().BeTrue(
            "a run parked on the master's question inside the fix pass resumes inside it");
        PhaseReviewLedger.ForThisPhase(resumed).Findings.Should().ContainSingle();
        PhaseReviewFixPass.RestoreSnapshot(resumed);
        resumed.Get<IReadOnlyList<SpecAccount>>(ContextKeys.PhaseAccounts)[0].Problem.Should().Be("first");
        RunAccountLedger.Current(resumed).All[0].Problem.Should().Be("first");
    }

    [Fact]
    public void CodePhaseBlock_ReviewFollowsVerifyAndPrecedesTheRecord()
    {
        var block = PipelinePresets.CodePhaseBlock.ToList();

        block.IndexOf(CommandNames.ReviewPhaseDiff).Should().BeGreaterThan(
            block.IndexOf(CommandNames.VerifyPhase), "the review reads VERIFIED work");
        block.IndexOf(CommandNames.ReviewPhaseDiff).Should().BeLessThan(
            block.IndexOf(CommandNames.WritePhaseRecord),
            "the record states what came of the phase, findings included");
    }

    private static PhaseReviewReport Found() => PhaseReviewReport.Taken([Finding()]);

    private static PhaseFinding Finding() =>
        new(Key, "src/Api/Handler.cs", 4, "files stay under 120 lines", "this one is 400", "P1");

    private static SpecAccount Account(string problem) => new(Key, [], problem);

    private static SpecAccount Delivered() =>
        new(Key, [new CriterionAccount(
            "the guard is in place", AccountDisposition.Satisfied, "src/Api/Handler.cs")]);

    private static IReadOnlyDictionary<string, ISandbox> Sandboxes(ISandbox sandbox) =>
        new Dictionary<string, ISandbox>(StringComparer.Ordinal) { [Key] = sandbox };

    /// <summary>A pipeline standing where a green verification leaves one.</summary>
    private static PipelineContext Verified()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.PhaseSpec, new PhaseDraft(PhaseId, "goal", "phase: x\n", []));
        pipeline.Set<IReadOnlyDictionary<string, string>>(
            ContextKeys.VerifiedHeads,
            new Dictionary<string, string>(StringComparer.Ordinal) { [Key] = "verified-sha" });
        return pipeline;
    }

    /// <summary>
    /// …and then inside the one fix pass, with a sandbox the revert can reach. The account is
    /// seeded BEFORE the pass, because that is what the snapshot has to put back.
    /// </summary>
    private static PipelineContext InTheFixPass(ISandbox sandbox, SpecAccount? verified = null)
    {
        var pipeline = Verified();
        if (verified is not null)
        {
            pipeline.Set<IReadOnlyList<SpecAccount>>(ContextKeys.PhaseAccounts, [verified]);
            RunAccountLedger.Record(pipeline, [verified]);
        }
        PhaseReviewLedger.Record(pipeline, Found());
        PhaseReviewFixPass.Prepare(pipeline);
        pipeline.Set(ContextKeys.Sandboxes, Sandboxes(sandbox));
        pipeline.Set<IReadOnlyDictionary<string, RemoteContextDiscovery>>(
            ContextKeys.SandboxDiscoveries, new Dictionary<string, RemoteContextDiscovery>());
        return pipeline;
    }
}
