using AgentSmith.Application.Services.Configuration;
using AgentSmith.Contracts.Models.Configuration;
using FluentAssertions;

namespace AgentSmith.Tests.Configuration;

/// <summary>
/// Covers p0139's AgentSmithConfigValidator: it runs after the catalog
/// resolver and catches cross-cutting mistakes (pipeline_triggers values
/// referencing unknown pipelines, trigger-block kind mismatched against
/// the resolved tracker type).
/// </summary>
public sealed class AgentSmithConfigValidatorTests
{
    private readonly AgentSmithConfigValidator _sut = new();

    [Fact]
    public void Validate_PipelineTriggersUsesKnownPipeline_NoError()
    {
        var config = new AgentSmithConfig
        {
            PipelineTriggers = new PipelineTriggerMap(
                new Dictionary<string, string> { ["bug"] = "fix-bug" }),
        };

        _sut.Validate(config).Should().BeEmpty();
    }

    [Fact]
    public void Validate_PipelineTriggersReferencesUnknownPipeline_ReturnsError()
    {
        var config = new AgentSmithConfig
        {
            PipelineTriggers = new PipelineTriggerMap(
                new Dictionary<string, string> { ["custom"] = "made-up-pipeline" }),
        };

        var errors = _sut.Validate(config);

        errors.Should().ContainSingle(e =>
            e.Contains("pipeline_triggers['custom']") &&
            e.Contains("made-up-pipeline"));
    }

    [Fact]
    public void Validate_JiraTriggerOnJiraTracker_NoError()
    {
        var config = ConfigWithProject(
            "p",
            new TrackerConnection { Name = "t", Type = TrackerType.Jira },
            project => project with { JiraTrigger = new JiraTriggerConfig() });

        _sut.Validate(config).Should().BeEmpty();
    }

    [Fact]
    public void Validate_AzureDevOpsTriggerOnJiraTracker_ReturnsError()
    {
        var config = ConfigWithProject(
            "p",
            new TrackerConnection { Name = "jira-prod", Type = TrackerType.Jira },
            project => project with { AzuredevopsTrigger = new WebhookTriggerConfig() });

        var errors = _sut.Validate(config);

        errors.Should().Contain(e =>
            e.Contains("azuredevops_trigger") &&
            e.Contains("jira-prod") &&
            e.Contains("Jira"));
    }

    [Fact]
    public void Validate_GitHubTriggerOnGitHubTracker_NoError()
    {
        var config = ConfigWithProject(
            "p",
            new TrackerConnection { Name = "gh", Type = TrackerType.GitHub },
            project => project with { GithubTrigger = new WebhookTriggerConfig() });

        _sut.Validate(config).Should().BeEmpty();
    }

    private static AgentSmithConfig ConfigWithProject(
        string name, TrackerConnection tracker, Func<ResolvedProject, ResolvedProject> shape) =>
        new()
        {
            Projects = new()
            {
                [name] = shape(new ResolvedProject { Name = name, Tracker = tracker }),
            },
        };
}
