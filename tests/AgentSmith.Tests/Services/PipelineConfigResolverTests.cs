using AgentSmith.Application.Services;
using AgentSmith.Contracts.Models.Configuration;
using FluentAssertions;

namespace AgentSmith.Tests.Services;

public class PipelineConfigResolverTests
{
    private readonly PipelineConfigResolver _sut = new();

    [Fact]
    public void Resolve_PipelineWithoutOverrides_ReturnsProjectDefaults()
    {
        var project = new ProjectConfig
        {
            Agent = new AgentConfig { Type = "Claude", Model = "sonnet" },
            CodingPrinciplesPath = ".agentsmith/coding-principles.md",
            Pipelines = [new PipelineDefinition { Name = "fix-bug" }],
        };

        var resolved = _sut.Resolve(project, "fix-bug");

        resolved.PipelineName.Should().Be("fix-bug");
        resolved.Agent.Type.Should().Be("Claude");
        resolved.SkillsPath.Should().Be("skills/coding");
        resolved.CodingPrinciplesPath.Should().Be(".agentsmith/coding-principles.md");
    }

    [Fact]
    public void Resolve_PipelineWithAgentOverride_MergesOverrideOnTop()
    {
        var baseAgent = new AgentConfig { Type = "Claude", Model = "sonnet" };
        var overrideAgent = new AgentConfig { Type = "OpenAI", Model = "gpt-4.1" };
        var project = new ProjectConfig
        {
            Agent = baseAgent,
            Pipelines = [new PipelineDefinition { Name = "security-scan", Agent = overrideAgent }],
        };

        var resolved = _sut.Resolve(project, "security-scan");

        resolved.Agent.Type.Should().Be("OpenAI");
    }

    [Fact]
    public void Resolve_PipelineWithSkillsPathOverride_ReturnsExplicitPath()
    {
        var project = new ProjectConfig
        {
            Pipelines =
            [
                new PipelineDefinition { Name = "security-scan", SkillsPath = "skills/my-custom-security" }
            ],
        };

        var resolved = _sut.Resolve(project, "security-scan");

        resolved.SkillsPath.Should().Be("skills/my-custom-security");
    }

    [Fact]
    public void Resolve_NoExplicitSkillsPath_FallsBackToPipelinePresetsDefault()
    {
        var project = new ProjectConfig
        {
            Pipelines = [new PipelineDefinition { Name = "security-scan" }],
        };

        var resolved = _sut.Resolve(project, "security-scan");

        resolved.SkillsPath.Should().Be("skills/security");
    }

    [Fact]
    public void Resolve_UnknownPipelineWithoutPreset_FallsBackToCodingDefault()
    {
        var project = new ProjectConfig
        {
            Pipelines = [new PipelineDefinition { Name = "fix-bug" }],
        };

        var resolved = _sut.Resolve(project, "completely-unknown");

        resolved.SkillsPath.Should().Be("skills/coding");
    }

    [Fact]
    public void Resolve_LegacyPipelineString_TranslatesToSinglePipeline()
    {
        var project = new ProjectConfig
        {
            Agent = new AgentConfig { Type = "Claude" },
            Pipeline = "fix-bug",
        };

        var resolved = _sut.Resolve(project, "fix-bug");

        resolved.PipelineName.Should().Be("fix-bug");
        resolved.Agent.Type.Should().Be("Claude");
        resolved.SkillsPath.Should().Be("skills/coding");
    }

    [Fact]
    public void Resolve_LegacyConfigCustomSkillsPath_CarriedThroughResolution()
    {
        var project = new ProjectConfig
        {
            Pipeline = "security-scan",
            SkillsPath = "skills/security",
        };

        var resolved = _sut.Resolve(project, "security-scan");

        resolved.SkillsPath.Should().Be("skills/security");
    }

    [Fact]
    public void ResolveDefaultPipelineName_DefaultPipelineSet_ReturnsIt()
    {
        var project = new ProjectConfig
        {
            DefaultPipeline = "security-scan",
            Pipelines =
            [
                new PipelineDefinition { Name = "fix-bug" },
                new PipelineDefinition { Name = "security-scan" },
            ],
        };

        _sut.ResolveDefaultPipelineName(project).Should().Be("security-scan");
    }

    [Fact]
    public void ResolveDefaultPipelineName_SinglePipelineNoDefault_ReturnsIt()
    {
        var project = new ProjectConfig
        {
            Pipelines = [new PipelineDefinition { Name = "fix-bug" }],
        };

        _sut.ResolveDefaultPipelineName(project).Should().Be("fix-bug");
    }

    [Fact]
    public void ResolveDefaultPipelineName_LegacyOnly_ReturnsLegacyPipeline()
    {
        var project = new ProjectConfig { Pipeline = "fix-bug" };

        _sut.ResolveDefaultPipelineName(project).Should().Be("fix-bug");
    }

    [Fact]
    public void ResolveDefaultPipelineName_MultiplePipelinesNoDefault_Throws()
    {
        var project = new ProjectConfig
        {
            Pipelines =
            [
                new PipelineDefinition { Name = "fix-bug" },
                new PipelineDefinition { Name = "security-scan" },
            ],
        };

        Action act = () => _sut.ResolveDefaultPipelineName(project);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*no default_pipeline*");
    }
}
