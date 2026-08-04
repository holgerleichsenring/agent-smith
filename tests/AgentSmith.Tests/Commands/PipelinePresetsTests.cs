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
    public void Code_IsTheOneCodeChangingPreset_AndGatesBeforeThePullRequest()
    {
        // p0393: fix-bug, fix-no-test, add-feature and phase-execution collapsed into one.
        // The order that matters: the ticket becomes specs, the gate runs before any master
        // token is spent, and VerifyPhase sits before CommitAndPR so a red build cannot open
        // a pull request. p0393a: master/verify are spliced per derived phase, so the
        // order is asserted over the EFFECTIVE list — the steps a run actually executes.
        PipelinePresets.Effective(PipelinePresets.CodeName).Should().ContainInOrder(
            CommandNames.AnalyzeCode,
            CommandNames.DeriveSpec,
            CommandNames.PhaseSpecGate,
            CommandNames.SelectPhase,
            CommandNames.AgenticMaster,
            CommandNames.VerifyPhase,
            CommandNames.CommitAndPR);
    }

    [Fact]
    public void PhaseSequence_Splice_ContainsNoPlanCommands()
    {
        // p0394a: the ratified phase spec is the run's single planning artifact — the
        // legacy JSON plan call and its plan-level clarification gate left the phase
        // path. The block the sequence splices per phase must not re-introduce them.
        PipelinePresets.CodePhaseBlock.Should().NotContain("GeneratePlanCommand");
        PipelinePresets.CodePhaseBlock.Should().NotContain("PlanOpenQuestionsCommand");
        PipelinePresets.Effective(PipelinePresets.CodeName)
            .Should().NotContain("GeneratePlanCommand")
            .And.NotContain("PlanOpenQuestionsCommand");
    }

    [Fact]
    public void Code_HasNeitherApprovalNorNegotiation_BecauseEveryRunNowCarriesASpec()
    {
        // Approval blocked the run on an operator who is not there — deleted outright.
        // p0393 had to keep NegotiateExpectation because only a PHASE ticket carried a
        // spec, so deleting it would have left ordinary tickets with an empty acceptance
        // contract. p0393a derives a spec for every ticket and the phase's done-list IS
        // that contract, so negotiating a second one restates the same intent a third time.
        var code = PipelinePresets.Effective(PipelinePresets.CodeName);

        code.Should().NotContain(CommandNames.Approval);
        code.Should().NotContain(CommandNames.NegotiateExpectation);
        code.Should().Contain(CommandNames.DeriveSpec);
        code.Should().Contain(CommandNames.SpecHandback);
        code.Should().Contain(CommandNames.MasterOpenQuestions);
    }

    [Theory]
    [InlineData("fix-bug")]
    [InlineData("fix-no-test")]
    [InlineData("add-feature")]
    [InlineData("phase-execution")]
    public void RetiredPresetName_ResolvesToCode_AndKeepsItsClassification(string alias)
    {
        // Renaming by alias, not by breaking configuration: preset names live in operator
        // triggers and projects. Resolving the command list while classifying the RAW name
        // would make the alias half-real — the run would execute `code` but be sized and
        // judged as something else, which is worse than not aliasing at all.
        PipelinePresets.TryResolve(alias).Should().BeSameAs(PipelinePresets.Code);
        PipelinePresets.Canonical(alias).Should().Be(PipelinePresets.CodeName);
        PipelinePresets.ExpectsCodeChanges(alias).Should().BeTrue();
        PipelinePresets.ExpectsGreenTests(alias).Should().BeTrue();
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
        PipelinePresets.Code.Should().NotContain("TestCommand");
        PipelinePresets.Code.Should().Contain(CommandNames.CommitAndPR);
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
    public void Code_HasNoFixedTestOrDocsStep()
    {
        // p0393: add-feature carried GenerateTests + GenerateDocs for every run whether the
        // work needed them or not. They are steps in the phase spec now — the decision moved
        // to where the work is described, instead of a preset name deciding it for everyone.
        PipelinePresets.Code.Should().NotContain(CommandNames.GenerateTests);
        PipelinePresets.Code.Should().NotContain(CommandNames.GenerateDocs);
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
        // p0393: `code` runs VerifyPhase, a DETERMINISTIC build+test gate. That is not the
        // retired RunVerifyPhase choreography step — it is the second opinion p0216 left
        // missing when it handed verification to the master as a responsibility.
        PipelinePresets.Code.Should().NotContain(CommandNames.RunVerifyPhase);
        // p0393a: VerifyPhase runs once per derived phase, spliced by PhaseSequence.
        PipelinePresets.Effective(PipelinePresets.CodeName).Should().Contain(CommandNames.VerifyPhase);
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
