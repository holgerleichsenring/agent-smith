using AgentSmith.Contracts.Commands;
using FluentAssertions;

namespace AgentSmith.Tests.Commands;

public class PipelinePresetsTests
{
    [Theory]
    [InlineData("fix-bug")]
    [InlineData("fix-no-test")]
    [InlineData("init-project")]
    [InlineData("add-feature")]
    public void TryResolve_KnownPreset_ReturnsCommands(string name)
    {
        var result = PipelinePresets.TryResolve(name);

        result.Should().NotBeNull();
        result!.Count.Should().BeGreaterThan(0);
    }

    [Fact]
    public void TryResolve_UnknownPreset_ReturnsNull()
    {
        PipelinePresets.TryResolve("nonexistent").Should().BeNull();
    }

    [Fact]
    public void TryResolve_CaseInsensitive()
    {
        PipelinePresets.TryResolve("Fix-Bug").Should().NotBeNull();
        PipelinePresets.TryResolve("INIT-PROJECT").Should().NotBeNull();
    }

    [Fact]
    public void FixBug_ContainsExpectedCommands()
    {
        PipelinePresets.FixBug.Should().Contain(CommandNames.FetchTicket);
        PipelinePresets.FixBug.Should().Contain(CommandNames.CommitAndPR);
        // p0216: the rigid projectmap-derived Test step was dropped — the
        // coding-agent-master owns build+test verification. The CommandNames.Test
        // constant is gone, so guard against its retired raw value.
        PipelinePresets.FixBug.Should().NotContain("TestCommand");
    }

    [Fact]
    public void FixBug_UsesAgenticMaster_WithGeneratePlanBeforeApproval()
    {
        // p0179b: coding pipelines collapse to one master step. p0276: GeneratePlan
        // is BACK before Approval so the plan is generated + approved BEFORE the
        // master executes it. p0318: PlanOpenQuestions (the clarification gate) is
        // BACK too, between GeneratePlan and Approval, so a title-only / needs-input
        // ticket halts before the master; the rest (Triage / EmptyPlanCheck / Run*Phase)
        // stays inside the master skill body.
        var fix = PipelinePresets.FixBug.ToList();
        fix.Should().Contain(CommandNames.AgenticMaster);
        fix.IndexOf(CommandNames.GeneratePlan).Should().BeGreaterThan(-1);
        fix.IndexOf(CommandNames.GeneratePlan).Should()
            .BeLessThan(fix.IndexOf(CommandNames.Approval), "the plan must be generated before the approval gate");
        // p0318: clarification gate sits GeneratePlan → PlanOpenQuestions → Approval.
        fix.IndexOf(CommandNames.PlanOpenQuestions).Should()
            .BeGreaterThan(fix.IndexOf(CommandNames.GeneratePlan), "the gate reads the generated plan");
        fix.IndexOf(CommandNames.PlanOpenQuestions).Should()
            .BeLessThan(fix.IndexOf(CommandNames.Approval), "the gate halts before approval/master");
        PipelinePresets.FixBug.Should().NotContain(CommandNames.Triage);
        PipelinePresets.FixBug.Should().NotContain(CommandNames.EmptyPlanCheck);
        PipelinePresets.FixBug.Should().NotContain(CommandNames.AgenticExecute);
        PipelinePresets.FixBug.Should().NotContain(CommandNames.RunReviewPhase);
        PipelinePresets.FixBug.Should().NotContain(CommandNames.RunFinalPhase);
        PipelinePresets.FixBug.Should().NotContain(CommandNames.RunVerifyPhase);
    }

    [Fact]
    public void FixBug_AndAddFeature_NegotiateExpectation_AfterAnalyzeBeforePlanning()
    {
        // p0328: the negotiation draft must be grounded in analysis (after
        // AnalyzeCode) and ratified before any planning/provisioning work
        // (before EnsurePrerequisites and GeneratePlan).
        foreach (var preset in new[] { PipelinePresets.FixBug.ToList(), PipelinePresets.AddFeature.ToList() })
        {
            var negotiate = preset.IndexOf(CommandNames.NegotiateExpectation);
            negotiate.Should().BeGreaterThan(preset.IndexOf(CommandNames.AnalyzeCode),
                "the draft is grounded in reproduction/analysis, not the raw ticket");
            negotiate.Should().BeLessThan(preset.IndexOf(CommandNames.EnsurePrerequisites),
                "the WHAT is ratified before provisioning/planning starts");
            negotiate.Should().BeLessThan(preset.IndexOf(CommandNames.GeneratePlan));
        }
    }

    [Fact]
    public void FixBug_ApprovalBeforeMaster_ThenWriteResultAndPr_NoPersistInHappyPath()
    {
        // p0216: the rigid Test step is gone; the master owns verification.
        // p0258: PersistWorkBranch is REMOVED from the happy path — it committed +
        // pushed the master's working changes, leaving the tree clean so CommitAndPR
        // saw hasCode=False ("recorded source edits but git committed NOTHING", no
        // PR). It is failure-recovery only (PipelineErrorHandler owns the WIP push).
        // The tail is now …→ AgenticMaster → WriteRunResult → CommitAndPR.
        var preset = PipelinePresets.FixBug.ToList();
        var approvalIdx = preset.IndexOf(CommandNames.Approval);
        var masterIdx = preset.IndexOf(CommandNames.AgenticMaster);
        var writeResultIdx = preset.IndexOf(CommandNames.WriteRunResult);
        var prIdx = preset.IndexOf(CommandNames.CommitAndPR);

        approvalIdx.Should().BeGreaterThan(-1);
        masterIdx.Should().BeGreaterThan(approvalIdx);
        writeResultIdx.Should().BeGreaterThan(masterIdx);
        prIdx.Should().BeGreaterThan(writeResultIdx);
        preset.Should().NotContain(CommandNames.PersistWorkBranch,
            "PersistWorkBranch is failure-recovery only; in the happy path it stole the master's changes from CommitAndPR");
    }

    [Fact]
    public void AddFeature_UsesAgenticMaster_WithGeneratePlanBeforeApproval()
    {
        // p0276: GeneratePlan re-introduced before Approval (see FixBug test).
        var add = PipelinePresets.AddFeature.ToList();
        add.Should().Contain(CommandNames.AgenticMaster);
        add.IndexOf(CommandNames.GeneratePlan).Should()
            .BeLessThan(add.IndexOf(CommandNames.Approval), "the plan must be generated before the approval gate");
        PipelinePresets.AddFeature.Should().NotContain(CommandNames.Triage);
        PipelinePresets.AddFeature.Should().NotContain(CommandNames.AgenticExecute);
        // GenerateTests + GenerateDocs stay — they are separate post-master responsibilities
        PipelinePresets.AddFeature.Should().Contain(CommandNames.GenerateTests);
        PipelinePresets.AddFeature.Should().Contain(CommandNames.GenerateDocs);
    }

    [Fact]
    public void FixNoTest_UsesAgenticMaster_NotTriageOrGeneratePlan()
    {
        PipelinePresets.FixNoTest.Should().Contain(CommandNames.AgenticMaster);
        PipelinePresets.FixNoTest.Should().NotContain(CommandNames.Triage);
        PipelinePresets.FixNoTest.Should().NotContain(CommandNames.GeneratePlan);
        PipelinePresets.FixNoTest.Should().NotContain(CommandNames.AgenticExecute);
        // FixNoTest never had a Test gate; p0216 dropped it from all coding presets.
        PipelinePresets.FixNoTest.Should().NotContain("TestCommand");
    }

    [Fact]
    public void ApiSecurityScan_FirstStepsAreLoadCatalogThenPipelineNameInitializer()
    {
        // p0205: LoadCatalog binds the skill catalog as the first visible step.
        // p0125c: PipelineNameInitializer then publishes the pipeline_name concept
        // before any other handler runs.
        PipelinePresets.ApiSecurityScan[0].Should().Be(CommandNames.LoadCatalog);
        PipelinePresets.ApiSecurityScan[1].Should().Be(CommandNames.PipelineNameInitializer);
        PipelinePresets.ApiSecurityScan[2].Should().Be(CommandNames.TryCheckoutSource);
    }

    [Fact]
    public void CodingPresets_DoNotContainTestCommand()
    {
        // p0216: the rigid projectmap-derived Test step ("TestCommand") was
        // removed from every coding preset; the coding-agent-master owns
        // build+test verification via its real run_command calls.
        PipelinePresets.FixBug.Should().NotContain("TestCommand");
        PipelinePresets.AddFeature.Should().NotContain("TestCommand");
        PipelinePresets.FixNoTest.Should().NotContain("TestCommand");
        PipelinePresets.FixNoTest.Should().Contain(CommandNames.CommitAndPR);
    }

    [Fact]
    public void InitProject_ContainsKeyCommands()
    {
        // p0130c: BootstrapProject retired from this preset; replaced by the
        // AnalyzeCode → PublishProjectLanguage → LoadSkills → BootstrapDispatch
        // chain. Full step sequence is asserted in InitProjectPipelinePresetTests.
        PipelinePresets.InitProject.Should().Contain(CommandNames.PipelineNameInitializer);
        PipelinePresets.InitProject.Should().Contain(CommandNames.CheckoutSource);
        PipelinePresets.InitProject.Should().Contain(CommandNames.BootstrapDispatch);
        PipelinePresets.InitProject.Should().Contain(CommandNames.InitCommit);
        PipelinePresets.InitProject.Should().NotContain(CommandNames.BootstrapProject);
    }

    [Fact]
    public void AddFeature_ContainsGenerateTestsAndDocs()
    {
        PipelinePresets.AddFeature.Should().Contain(CommandNames.GenerateTests);
        PipelinePresets.AddFeature.Should().Contain(CommandNames.GenerateDocs);
    }

    [Theory]
    [InlineData("security-scan")]
    [InlineData("api-security-scan")]
    [InlineData("legal-analysis")]
    public void ScanPresets_UseAgenticMaster_PostP0179d(string name)
    {
        var preset = PipelinePresets.TryResolve(name);
        preset.Should().Contain(CommandNames.AgenticMaster);
        preset.Should().NotContain(CommandNames.Triage);
        preset.Should().NotContain(CommandNames.ConvergenceCheck);
        preset.Should().NotContain(CommandNames.RunReviewPhase);
        preset.Should().NotContain(CommandNames.RunFinalPhase);
    }

    [Fact]
    public void MadDiscussion_UsesAgenticMaster_PostP0179e()
    {
        // p0179e: mad-discussion preset collapsed to one AgenticMaster step
        // that loads mad-discussion-master, which internally orchestrates
        // the 5 perspectives via spawn_agents.
        var preset = PipelinePresets.MadDiscussion;
        preset.Should().Contain(CommandNames.AgenticMaster);
        preset.Should().NotContain(CommandNames.Triage);
        preset.Should().NotContain(CommandNames.ConvergenceCheck);
        preset.Should().NotContain(CommandNames.CompileDiscussion);
    }

    [Fact]
    public void InitProject_DoesNotContainTriage()
    {
        PipelinePresets.InitProject.Should().NotContain(CommandNames.Triage);
    }

    [Fact]
    public void CodingPresets_DoNotContainRunVerifyPhase_PostP0179b()
    {
        // p0179b: RunVerifyPhase is part of the choreography the master skill
        // absorbs (Phase 3 — Verify in coding-agent-master).
        PipelinePresets.FixBug.Should().NotContain(CommandNames.RunVerifyPhase);
        PipelinePresets.AddFeature.Should().NotContain(CommandNames.RunVerifyPhase);
        PipelinePresets.FixNoTest.Should().NotContain(CommandNames.RunVerifyPhase);
    }

    [Fact]
    public void NonImplementationPresets_DoNotContainRunVerifyPhase()
    {
        PipelinePresets.SecurityScan.Should().NotContain(CommandNames.RunVerifyPhase);
        PipelinePresets.ApiSecurityScan.Should().NotContain(CommandNames.RunVerifyPhase);
        PipelinePresets.MadDiscussion.Should().NotContain(CommandNames.RunVerifyPhase);
        PipelinePresets.InitProject.Should().NotContain(CommandNames.RunVerifyPhase);
    }
}
