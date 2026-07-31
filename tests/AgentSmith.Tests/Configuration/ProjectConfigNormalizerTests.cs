using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using FluentAssertions;

namespace AgentSmith.Tests.Configuration;

public class ProjectConfigNormalizerTests
{
    private readonly StartupFindings _findings = new();
    private readonly ProjectConfigNormalizer _sut;

    public ProjectConfigNormalizerTests() => _sut = new ProjectConfigNormalizer(findings: _findings);

    [Fact]
    public void Normalize_LegacyPipelineString_TranslatesToPipelinesAndDefaultPipeline()
    {
        var project = new RawProjectEntry { Pipeline = "fix-bug" };

        _sut.Normalize("p", project);

        project.Pipelines.Should().HaveCount(1);
        project.Pipelines[0].Name.Should().Be("fix-bug");
        project.DefaultPipeline.Should().Be("fix-bug");
    }

    [Fact]
    public void Normalize_LegacySkillsPathDefaultValue_NotCarriedToPipelineDefinition()
    {
        var project = new RawProjectEntry { Pipeline = "fix-bug", SkillsPath = "skills/coding" };

        _sut.Normalize("p", project);

        project.Pipelines[0].SkillsPath.Should().BeNull();
    }

    [Fact]
    public void Normalize_LegacySkillsPathCustomValue_CarriedToPipelineDefinition()
    {
        var project = new RawProjectEntry { Pipeline = "security-scan", SkillsPath = "skills/security" };

        _sut.Normalize("p", project);

        project.Pipelines[0].SkillsPath.Should().Be("skills/security");
    }

    [Fact]
    public void Normalize_BothLegacyAndPipelinesSet_LegacyAddedAsAdditionalPipeline()
    {
        var project = new RawProjectEntry
        {
            Pipeline = "fix-bug",
            Pipelines = [new RawPipelineEntry { Name = "security-scan" }],
        };

        _sut.Normalize("p", project);

        project.Pipelines.Should().HaveCount(2);
        project.Pipelines.Should().Contain(p => p.Name == "fix-bug");
        project.Pipelines.Should().Contain(p => p.Name == "security-scan");
        project.DefaultPipeline.Should().Be("fix-bug");
    }

    [Fact]
    public void Normalize_LegacyPipelineAlreadyInPipelinesList_NotDuplicated()
    {
        var project = new RawProjectEntry
        {
            Pipeline = "fix-bug",
            Pipelines = [new RawPipelineEntry { Name = "fix-bug", SkillsPath = "skills/custom" }],
        };

        _sut.Normalize("p", project);

        project.Pipelines.Should().HaveCount(1);
        project.Pipelines[0].SkillsPath.Should().Be("skills/custom");
    }

    [Fact]
    public void Normalize_DefaultPipelineNotInPipelinesList_ReturnsBlockingFindingAndDoesNotThrow()
    {
        var project = new RawProjectEntry
        {
            DefaultPipeline = "missing",
            Pipelines = [new RawPipelineEntry { Name = "fix-bug" }],
        };

        Action act = () => _sut.Normalize("proj", project);

        act.Should().NotThrow();
        _findings.All.Should().ContainSingle(f =>
            f.IsBlocking && f.Project == "proj" && f.Field == "default_pipeline");
        _findings.All[0].Reason.Should().Contain("missing");
    }

    [Fact]
    public void Normalize_TriggerPipelineFromLabelReferencesUndeclaredPipeline_DoesNotThrow()
    {
        var project = new RawProjectEntry
        {
            Pipelines = [new RawPipelineEntry { Name = "fix-bug" }],
            GithubTrigger = new WebhookTriggerConfig
            {
                // p0391: fix-bug can park, so the trigger must name a park status — that is a
                // different rule, asserted below; this test is about label routing only.
                NeedsClarificationStatus = "Question",
                PipelineFromLabel = new Dictionary<string, string> { ["security-review"] = "security-scan" }
            }
        };

        Action act = () => _sut.Normalize("proj", project);

        act.Should().NotThrow();
    }

    // ---- p0391: a preset that can park must have somewhere to park ----

    [Fact]
    public void Normalizer_ParkingPresetWithoutStatus_ReturnsBlockingFindingAndDoesNotThrow()
    {
        // The silent degrade this replaces: the gate posted its question, logged
        // "(not parked — needs_clarification_status unset)" and ended the run Ok, leaving the
        // ticket in a trigger status — so discovery re-claimed it and the same run repeated.
        var project = new RawProjectEntry
        {
            Pipelines = [new RawPipelineEntry { Name = "fix-bug" }],
            GithubTrigger = new WebhookTriggerConfig { TriggerStatuses = ["open"] },
        };

        Action act = () => _sut.Normalize("proj", project);

        act.Should().NotThrow();
        var finding = _findings.All.Should().ContainSingle().Which;
        finding.IsBlocking.Should().BeTrue();
        finding.Project.Should().Be("proj");
        finding.Trigger.Should().Be("github_trigger");
        finding.Field.Should().Be("needs_clarification_status");
        finding.Reason.Should().Contain("fix-bug");
    }

    [Fact]
    public void Normalize_ParkingPipelineReachedOnlyByLabelRoute_ReturnsBlockingFinding()
    {
        // The pipeline a ticket actually runs can come from the label map, not from
        // pipelines:/default_pipeline — the rule reads every route the trigger can take.
        var project = new RawProjectEntry
        {
            Pipelines = [new RawPipelineEntry { Name = "security-scan" }],
            DefaultPipeline = "security-scan",
            GithubTrigger = new WebhookTriggerConfig
            {
                TriggerStatuses = ["open"],
                PipelineFromLabel = new Dictionary<string, string> { ["bug"] = "add-feature" },
            },
        };

        Action act = () => _sut.Normalize("proj", project);

        act.Should().NotThrow();
        _findings.All.Should().ContainSingle(f => f.IsBlocking && f.Reason.Contains("add-feature"));
    }

    [Fact]
    public void Normalize_NonParkingPipelineWithoutClarificationStatus_ProducesNoFinding()
    {
        // The rule fires only where a park can happen. A scan-only project has no
        // clarification step in its preset and is untouched.
        var project = new RawProjectEntry
        {
            Pipelines = [new RawPipelineEntry { Name = "security-scan" }],
            DefaultPipeline = "security-scan",
            GithubTrigger = new WebhookTriggerConfig { TriggerStatuses = ["open"] },
        };

        Action act = () => _sut.Normalize("proj", project);

        act.Should().NotThrow();
    }

    [Fact]
    public void Normalize_ParkingPipelineWithoutAnyTrigger_ProducesNoFinding()
    {
        // No trigger block = no tracker-driven runs = nothing to park. CLI-only projects
        // and the many trackerless test/demo configs stay valid.
        var project = new RawProjectEntry { Pipelines = [new RawPipelineEntry { Name = "fix-bug" }] };

        Action act = () => _sut.Normalize("proj", project);

        act.Should().NotThrow();
    }

    [Fact]
    public void Normalize_PipelinesAlreadySetAndNoLegacy_NoChange()
    {
        var project = new RawProjectEntry
        {
            Pipelines =
            [
                new RawPipelineEntry { Name = "a" },
                new RawPipelineEntry { Name = "b" },
            ],
        };

        _sut.Normalize("p", project);

        project.Pipelines.Should().HaveCount(2);
        project.DefaultPipeline.Should().BeNull();
    }

    // p0261: done_status/failed_status must be OUTSIDE trigger_statuses, else a
    // terminalized ticket lands back in a trigger status and is re-claimed forever.
    [Fact]
    public void Normalizer_TerminalStatusInsideTriggerStatuses_StillReturnsAFinding()
    {
        var project = new RawProjectEntry
        {
            AzuredevopsTrigger = new WebhookTriggerConfig
            {
                TriggerStatuses = ["New", "Active"],
                DoneStatus = "Active",
            },
        };

        var act = () => _sut.Normalize("p", project);

        act.Should().NotThrow();
        var finding = _findings.All.Should().ContainSingle().Which;
        finding.IsBlocking.Should().BeTrue();
        finding.Field.Should().Be("done_status");
        finding.Trigger.Should().Be("azuredevops_trigger");
        finding.Reason.Should().Contain("trigger_status");
    }

    [Fact]
    public void Normalize_FailedStatusInTriggerStatuses_ReturnsBlockingFinding()
    {
        var project = new RawProjectEntry
        {
            AzuredevopsTrigger = new WebhookTriggerConfig
            {
                TriggerStatuses = ["New", "Active"],
                DoneStatus = "Resolved",
                FailedStatus = "New",
            },
        };

        var act = () => _sut.Normalize("p", project);

        act.Should().NotThrow();
        _findings.All.Should().ContainSingle(f => f.IsBlocking && f.Field == "failed_status");
    }

    [Fact]
    public void Config_FullyValid_ProducesNoFindings()
    {
        var project = new RawProjectEntry
        {
            AzuredevopsTrigger = new WebhookTriggerConfig
            {
                TriggerStatuses = ["New", "Active"],
                DoneStatus = "Resolved",
                FailedStatus = "Blocked",
            },
        };

        var act = () => _sut.Normalize("p", project);

        act.Should().NotThrow();
        _findings.All.Should().BeEmpty();
    }

    [Fact]
    public void Normalize_EmptyTriggerStatuses_ReturnsAdvisoryFindingOnly()
    {
        var project = new RawProjectEntry
        {
            Pipelines = [new RawPipelineEntry { Name = "security-scan" }],
            AzuredevopsTrigger = new WebhookTriggerConfig { TriggerStatuses = [] },
        };

        _sut.Normalize("p", project);

        var finding = _findings.All.Should().ContainSingle().Which;
        finding.Severity.Should().Be(StartupFindingSeverity.Advisory);
        finding.Field.Should().Be("trigger_statuses");
    }

    [Fact]
    public void Normalize_OneBrokenTriggerAmongSeveral_OnlyThatTriggerIsNamed()
    {
        var project = new RawProjectEntry
        {
            Pipelines = [new RawPipelineEntry { Name = "fix-bug" }],
            GithubTrigger = new WebhookTriggerConfig { TriggerStatuses = ["open"] },
            JiraTrigger = new JiraTriggerConfig
            {
                TriggerStatuses = ["To Do"],
                NeedsClarificationStatus = "Question",
            },
        };

        _sut.Normalize("proj", project);

        _findings.All.Should().ContainSingle().Which.Trigger.Should().Be("github_trigger");
    }
}
