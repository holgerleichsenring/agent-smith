using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Server.Services.Webhooks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Webhooks;

public sealed class GitHubPrLabelWebhookHandlerTests
{
    private const string ConfigPath = "test-config.yml";

    private static readonly IDictionary<string, string> EmptyHeaders =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    // 2026-09-25-d83b: the trigger word comes from the config the handler reads, so the
    // handler needs a loader even for the deployment that configures nothing.
    private static GitHubPrLabelWebhookHandler CreateHandler()
    {
        var loader = new Mock<IConfigurationLoader>();
        loader.Setup(c => c.LoadConfig(ConfigPath)).Returns(new AgentSmithConfig());
        return new GitHubPrLabelWebhookHandler(
            loader.Object, new ServerContext(ConfigPath), new PrTriggerLabelResolver(),
            NullLogger<GitHubPrLabelWebhookHandler>.Instance);
    }

    [Fact]
    public async Task HandleAsync_LabeledSecurityReview_ReturnsSecurityScan()
    {
        var sut = CreateHandler();
        var payload = """
        {
            "action": "labeled",
            "label": { "name": "security-review" },
            "pull_request": { "number": 7 },
            "repository": { "name": "my-api", "clone_url": "https://github.com/org/my-api.git" }
        }
        """;

        var result = await sut.HandleAsync(payload, EmptyHeaders);

        result.Handled.Should().BeTrue();
        result.TriggerInput.Should().Contain("my-api");
        result.Pipeline.Should().Be("security-scan");
    }

    [Fact]
    public void CanHandle_CorrectPlatform()
    {
        var sut = CreateHandler();

        sut.CanHandle("github", "pull_request").Should().BeTrue();
        sut.CanHandle("github", "issues").Should().BeFalse();
    }
}
