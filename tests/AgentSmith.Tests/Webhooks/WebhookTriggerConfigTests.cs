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
        // 2026-09-16-a4d7: the literal initialiser was the defect — it made "this trigger
        // declares no fallback" inexpressible, so no merge could fill it and no operator
        // could see it. The answer for an undeclared trigger is PipelineResolver's.
        baseConfig.DefaultPipeline.Should().BeNull();
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
        var project = new ResolvedProject
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
