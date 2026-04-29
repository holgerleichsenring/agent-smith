using AgentSmith.Server.Services.Webhooks;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Webhooks;

public sealed class AzureDevOpsWorkItemWebhookHandlerTests
{
    private const string ConfigPath = "test-config.yml";

    private static readonly IDictionary<string, string> EmptyHeaders =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    private static AzureDevOpsWorkItemWebhookHandler CreateHandler(AgentSmithConfig? config = null)
    {
        var loader = new Mock<IConfigurationLoader>();
        loader.Setup(c => c.LoadConfig(ConfigPath)).Returns(config ?? BuildConfig());
        return new AzureDevOpsWorkItemWebhookHandler(loader.Object, new ServerContext(ConfigPath),
            NullLogger<AzureDevOpsWorkItemWebhookHandler>.Instance);
    }

    private static AgentSmithConfig BuildConfig() =>
        new()
        {
            Projects = new Dictionary<string, ProjectConfig>
            {
                ["my-project"] = new() { Tickets = new TicketConfig { Type = "AzureDevOps" } }
            }
        };

    [Fact]
    public async Task HandleAsync_TaggedSecurityReview_ReturnsSecurityScan()
    {
        var sut = CreateHandler();
        var payload = """
        {
            "resource": {
                "id": 99,
                "fields": { "System.Tags": "security-review; urgent" }
            },
            "resourceContainers": {
                "project": { "id": "my-project" }
            }
        }
        """;

        var result = await sut.HandleAsync(payload, EmptyHeaders);

        result.Handled.Should().BeTrue();
        result.Pipeline.Should().Be("security-scan");
        result.ProjectName.Should().Be("my-project");
        result.TicketId.Should().Be("99");
    }

    [Fact]
    public async Task HandleAsync_NoTag_ReturnsNotHandled()
    {
        var sut = CreateHandler();
        var payload = """
        {
            "resource": {
                "id": 99,
                "fields": { "System.Tags": "bug; P1" }
            },
            "resourceContainers": {
                "project": { "id": "my-project" }
            }
        }
        """;

        var result = await sut.HandleAsync(payload, EmptyHeaders);

        result.Handled.Should().BeFalse();
    }

    [Fact]
    public void CanHandle_CorrectPlatform()
    {
        var sut = CreateHandler();

        sut.CanHandle("azuredevops", "workitem.updated").Should().BeTrue();
        sut.CanHandle("azuredevops", "build.complete").Should().BeFalse();
    }
}
