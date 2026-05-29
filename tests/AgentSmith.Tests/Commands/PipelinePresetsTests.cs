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
        PipelinePresets.FixBug.Should().Contain(CommandNames.Test);
        PipelinePresets.FixBug.Should().Contain(CommandNames.CommitAndPR);
    }

    [Fact]
    public void FixBug_UsesAgenticMaster_NotTriageOrGeneratePlan()
    {
        // p0179b: coding pipelines collapse to one master step. The
        // choreography (Triage / GeneratePlan / PlanOpenQuestions /
        // EmptyPlanCheck / RunReviewPhase / RunFinalPhase / RunVerifyPhase)
        // moves inside the coding-agent-master skill body.
        PipelinePresets.FixBug.Should().Contain(CommandNames.AgenticMaster);
        PipelinePresets.FixBug.Should().NotContain(CommandNames.Triage);
        PipelinePresets.FixBug.Should().NotContain(CommandNames.GeneratePlan);
        PipelinePresets.FixBug.Should().NotContain(CommandNames.PlanOpenQuestions);
        PipelinePresets.FixBug.Should().NotContain(CommandNames.EmptyPlanCheck);
        PipelinePresets.FixBug.Should().NotContain(CommandNames.AgenticExecute);
        PipelinePresets.FixBug.Should().NotContain(CommandNames.RunReviewPhase);
        PipelinePresets.FixBug.Should().NotContain(CommandNames.RunFinalPhase);
        PipelinePresets.FixBug.Should().NotContain(CommandNames.RunVerifyPhase);
    }

    [Fact]
    public void FixBug_KeepsApprovalBeforeAgenticMaster_AndTestAfter()
    {
        var preset = PipelinePresets.FixBug.ToList();
        var approvalIdx = preset.IndexOf(CommandNames.Approval);
        var masterIdx = preset.IndexOf(CommandNames.AgenticMaster);
        var testIdx = preset.IndexOf(CommandNames.Test);

        approvalIdx.Should().BeGreaterThan(-1);
        masterIdx.Should().BeGreaterThan(approvalIdx);
        testIdx.Should().BeGreaterThan(masterIdx);
    }

    [Fact]
    public void AddFeature_UsesAgenticMaster_NotTriageOrGeneratePlan()
    {
        PipelinePresets.AddFeature.Should().Contain(CommandNames.AgenticMaster);
        PipelinePresets.AddFeature.Should().NotContain(CommandNames.Triage);
        PipelinePresets.AddFeature.Should().NotContain(CommandNames.GeneratePlan);
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
        // FixNoTest's whole point is skipping the Test gate
        PipelinePresets.FixNoTest.Should().NotContain(CommandNames.Test);
    }

    [Fact]
    public void ApiSecurityScan_FirstStepIsPipelineNameInitializer()
    {
        // p0125c: PipelineNameInitializer is prepended to every preset to publish
        // the pipeline_name concept once before any other handler runs.
        PipelinePresets.ApiSecurityScan[0].Should().Be(CommandNames.PipelineNameInitializer);
        PipelinePresets.ApiSecurityScan[1].Should().Be(CommandNames.TryCheckoutSource);
    }

    [Fact]
    public void FixNoTest_DoesNotContainTestCommand()
    {
        PipelinePresets.FixNoTest.Should().NotContain(CommandNames.Test);
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
    [InlineData("api-security-scan")]
    [InlineData("security-scan")]
    [InlineData("mad-discussion")]
    [InlineData("legal-analysis")]
    public void NonCodingPresets_StillContainTriage_UntilSliceDeMigratesThem(string name)
    {
        // p0179b: Triage retired from coding presets (fix-bug / add-feature /
        // fix-no-test) but stays in scan / mad / legal until p0179d (scan) and
        // p0179e (mad) replace their consumers. legal-analysis migrates as
        // part of p0179d.
        var preset = PipelinePresets.TryResolve(name);
        preset.Should().Contain(CommandNames.Triage);
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
