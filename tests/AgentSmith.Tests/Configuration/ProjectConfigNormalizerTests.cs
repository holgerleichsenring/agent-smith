using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using FluentAssertions;

namespace AgentSmith.Tests.Configuration;

public class ProjectConfigNormalizerTests
{
    private readonly ProjectConfigNormalizer _sut = new();

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
    public void Normalize_DefaultPipelineNotInPipelinesList_ThrowsConfigurationException()
    {
        var project = new RawProjectEntry
        {
            DefaultPipeline = "missing",
            Pipelines = [new RawPipelineEntry { Name = "fix-bug" }],
        };

        Action act = () => _sut.Normalize("proj", project);

        act.Should().Throw<ConfigurationException>()
            .WithMessage("*default_pipeline*missing*");
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
    public void Normalize_ParkingPipelineWithoutClarificationStatus_ThrowsAtLoad()
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

        act.Should().Throw<ConfigurationException>()
            .WithMessage("*needs_clarification_status*")
            .WithMessage("*fix-bug*");
    }

    [Fact]
    public void Normalize_ParkingPipelineReachedOnlyByLabelRoute_ThrowsAtLoad()
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

        act.Should().Throw<ConfigurationException>().WithMessage("*add-feature*");
    }

    [Fact]
    public void Normalize_NonParkingPipelineWithoutClarificationStatus_DoesNotThrow()
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
    public void Normalize_ParkingPipelineWithoutAnyTrigger_DoesNotThrow()
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
    public void Normalize_DoneStatusInTriggerStatuses_Throws()
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

        act.Should().Throw<ConfigurationException>().WithMessage("*done_status*trigger_status*");
    }

    [Fact]
    public void Normalize_FailedStatusInTriggerStatuses_Throws()
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

        act.Should().Throw<ConfigurationException>().WithMessage("*failed_status*trigger_status*");
    }

    [Fact]
    public void Normalize_TerminalStatusesOutsideTriggerStatuses_DoesNotThrow()
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
    }
}
