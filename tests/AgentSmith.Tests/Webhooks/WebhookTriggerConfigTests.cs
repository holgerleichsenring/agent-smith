using AgentSmith.Contracts.Models.Configuration;
using FluentAssertions;

namespace AgentSmith.Tests.Webhooks;

public sealed class WebhookTriggerConfigTests
{
    [Fact]
    public void JiraTriggerConfig_InheritsFromWebhookTriggerConfig()
    {
        var jiraConfig = new JiraTriggerConfig();

        // JiraTriggerConfig should be assignable to WebhookTriggerConfig
        WebhookTriggerConfig baseConfig = jiraConfig;
        baseConfig.Should().NotBeNull();
        baseConfig.DefaultPipeline.Should().Be("fix-bug");
        baseConfig.DoneStatus.Should().Be("In Review");
    }

    [Fact]
    public void JiraTriggerConfig_DefaultTriggerStatuses_IsOpen()
    {
        var config = new JiraTriggerConfig();
        config.TriggerStatuses.Should().Contain("Open");
    }

    [Fact]
    public void WebhookTriggerConfig_DefaultTriggerStatuses_IsEmpty()
    {
        var config = new WebhookTriggerConfig();
        config.TriggerStatuses.Should().BeEmpty();
    }

    [Fact]
    public void ProjectConfig_HasAllTriggerProperties()
    {
        var project = new ProjectConfig
        {
            GithubTrigger = new WebhookTriggerConfig
            {
                PipelineFromLabel = new Dictionary<string, string> { ["bug"] = "fix-bug" },
                TriggerStatuses = ["open"],
                DoneStatus = "closed"
            },
            GitlabTrigger = new WebhookTriggerConfig
            {
                PipelineFromLabel = new Dictionary<string, string> { ["security"] = "security-scan" },
                TriggerStatuses = ["opened"]
            },
            AzuredevopsTrigger = new WebhookTriggerConfig
            {
                PipelineFromLabel = new Dictionary<string, string> { ["security-review"] = "security-scan" },
                TriggerStatuses = ["New", "Active"]
            },
            JiraTrigger = new JiraTriggerConfig
            {
                AssigneeName = "Agent Smith"
            }
        };

        project.GithubTrigger.Should().NotBeNull();
        project.GitlabTrigger.Should().NotBeNull();
        project.AzuredevopsTrigger.Should().NotBeNull();
        project.JiraTrigger.Should().NotBeNull();
    }
}
