using AgentSmith.Server.Services.Webhooks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Webhooks;

public sealed class GitHubPrLabelWebhookHandlerTests
{
    private static readonly IDictionary<string, string> EmptyHeaders =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    [Fact]
    public async Task HandleAsync_LabeledSecurityReview_ReturnsSecurityScan()
    {
        var sut = new GitHubPrLabelWebhookHandler(NullLogger<GitHubPrLabelWebhookHandler>.Instance);
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
        var sut = new GitHubPrLabelWebhookHandler(NullLogger<GitHubPrLabelWebhookHandler>.Instance);

        sut.CanHandle("github", "pull_request").Should().BeTrue();
        sut.CanHandle("github", "issues").Should().BeFalse();
    }
}
