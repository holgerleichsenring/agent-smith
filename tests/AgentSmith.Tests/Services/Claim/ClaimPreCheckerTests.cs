using AgentSmith.Application.Services.Claim;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Services.Claim;

public sealed class ClaimPreCheckerTests
{
    [Fact]
    public void Check_UnknownProject_ReturnsUnknownProject()
    {
        var config = new AgentSmithConfig();
        var request = NewRequest("GitHub", "missing-project", "fix-bug");

        var rejection = ClaimPreChecker.Check(request, config);

        rejection.Should().Be(ClaimRejectionReason.UnknownProject);
    }

    [Fact]
    public void Check_UnknownPipeline_ReturnsUnknownPipeline()
    {
        var config = ConfigWithTrigger(pipeline: "fix-bug");
        var request = NewRequest("GitHub", "my-project", "nonexistent-pipeline");

        var rejection = ClaimPreChecker.Check(request, config);

        rejection.Should().Be(ClaimRejectionReason.UnknownPipeline);
    }

    [Fact]
    public void Check_PipelineNotLabelTriggered_ReturnsThatReason()
    {
        var config = ConfigWithTrigger(pipeline: "fix-bug");
        // security-scan exists in PipelinePresets but is not in the trigger config
        var request = NewRequest("GitHub", "my-project", "security-scan");

        var rejection = ClaimPreChecker.Check(request, config);

        rejection.Should().Be(ClaimRejectionReason.PipelineNotLabelTriggered);
    }

    [Fact]
    public void Check_ValidRequest_ReturnsNull()
    {
        var config = ConfigWithTrigger(pipeline: "fix-bug");
        var request = NewRequest("GitHub", "my-project", "fix-bug");

        var rejection = ClaimPreChecker.Check(request, config);

        rejection.Should().BeNull();
    }

    [Fact]
    public void Check_PipelineInPipelineFromLabel_PassesTriggerCheck()
    {
        var config = new AgentSmithConfig
        {
            Projects = new()
            {
                ["my-project"] = new ProjectConfig
                {
                    GithubTrigger = new WebhookTriggerConfig
                    {
                        DefaultPipeline = "fix-bug",
                        PipelineFromLabel = new() { ["secscan"] = "security-scan" }
                    }
                }
            }
        };
        var request = NewRequest("GitHub", "my-project", "security-scan");

        var rejection = ClaimPreChecker.Check(request, config);

        rejection.Should().BeNull();
    }

    [Fact]
    public void Check_UnknownPlatform_ReturnsNotLabelTriggered()
    {
        var config = ConfigWithTrigger(pipeline: "fix-bug");
        var request = NewRequest("MySpacePlatform", "my-project", "fix-bug");

        var rejection = ClaimPreChecker.Check(request, config);

        rejection.Should().Be(ClaimRejectionReason.PipelineNotLabelTriggered);
    }

    private static ClaimRequest NewRequest(string platform, string project, string pipeline)
        => new(platform, project, new TicketId("42"), pipeline);

    private static AgentSmithConfig ConfigWithTrigger(string pipeline) => new()
    {
        Projects = new()
        {
            ["my-project"] = new ProjectConfig
            {
                GithubTrigger = new WebhookTriggerConfig { DefaultPipeline = pipeline }
            }
        }
    };
}
