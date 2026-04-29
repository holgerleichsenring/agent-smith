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
        var project = new ProjectConfig { Pipeline = "fix-bug" };

        _sut.Normalize("p", project);

        project.Pipelines.Should().HaveCount(1);
        project.Pipelines[0].Name.Should().Be("fix-bug");
        project.DefaultPipeline.Should().Be("fix-bug");
    }

    [Fact]
    public void Normalize_LegacySkillsPathDefaultValue_NotCarriedToPipelineDefinition()
    {
        var project = new ProjectConfig { Pipeline = "fix-bug", SkillsPath = "skills/coding" };

        _sut.Normalize("p", project);

        project.Pipelines[0].SkillsPath.Should().BeNull();
    }

    [Fact]
    public void Normalize_LegacySkillsPathCustomValue_CarriedToPipelineDefinition()
    {
        var project = new ProjectConfig { Pipeline = "security-scan", SkillsPath = "skills/security" };

        _sut.Normalize("p", project);

        project.Pipelines[0].SkillsPath.Should().Be("skills/security");
    }

    [Fact]
    public void Normalize_BothLegacyAndPipelinesSet_PrefersExplicitWithoutSynth()
    {
        var project = new ProjectConfig
        {
            Pipeline = "fix-bug",
            Pipelines = [new PipelineDefinition { Name = "security-scan" }],
        };

        _sut.Normalize("p", project);

        project.Pipelines.Should().ContainSingle()
            .Which.Name.Should().Be("security-scan");
    }

    [Fact]
    public void Normalize_DefaultPipelineNotInPipelinesList_ThrowsConfigurationException()
    {
        var project = new ProjectConfig
        {
            DefaultPipeline = "missing",
            Pipelines = [new PipelineDefinition { Name = "fix-bug" }],
        };

        Action act = () => _sut.Normalize("proj", project);

        act.Should().Throw<ConfigurationException>()
            .WithMessage("*default_pipeline*missing*");
    }

    [Fact]
    public void Normalize_TriggerPipelineFromLabelReferencesUnknown_ThrowsConfigurationException()
    {
        var project = new ProjectConfig
        {
            Pipelines = [new PipelineDefinition { Name = "fix-bug" }],
            GithubTrigger = new WebhookTriggerConfig
            {
                PipelineFromLabel = new Dictionary<string, string> { ["security-review"] = "security-scan" }
            }
        };

        Action act = () => _sut.Normalize("proj", project);

        act.Should().Throw<ConfigurationException>()
            .WithMessage("*github_trigger*security-scan*");
    }

    [Fact]
    public void Normalize_TriggerDefaultPipelineNotDeclared_ThrowsConfigurationException()
    {
        var project = new ProjectConfig
        {
            Pipelines = [new PipelineDefinition { Name = "fix-bug" }],
            GithubTrigger = new WebhookTriggerConfig { DefaultPipeline = "ghost" }
        };

        Action act = () => _sut.Normalize("proj", project);

        act.Should().Throw<ConfigurationException>()
            .WithMessage("*github_trigger*ghost*");
    }

    [Fact]
    public void Normalize_PipelinesAlreadySetAndNoLegacy_NoChange()
    {
        var project = new ProjectConfig
        {
            Pipelines =
            [
                new PipelineDefinition { Name = "a" },
                new PipelineDefinition { Name = "b" },
            ],
        };

        _sut.Normalize("p", project);

        project.Pipelines.Should().HaveCount(2);
        project.DefaultPipeline.Should().BeNull();
    }
}
