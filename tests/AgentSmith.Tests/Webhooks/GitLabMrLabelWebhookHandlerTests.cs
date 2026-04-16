using AgentSmith.Cli.Services.Webhooks;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Webhooks;

public sealed class GitLabMrLabelWebhookHandlerTests
{
    private const string ConfigPath = "test-config.yml";

    private static readonly IDictionary<string, string> EmptyHeaders =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    private static GitLabMrLabelWebhookHandler CreateHandler(AgentSmithConfig? config = null)
    {
        var loader = new Mock<IConfigurationLoader>();
        loader.Setup(c => c.LoadConfig(ConfigPath)).Returns(config ?? BuildConfig());
        return new GitLabMrLabelWebhookHandler(loader.Object, new ServerContext(ConfigPath),
            NullLogger<GitLabMrLabelWebhookHandler>.Instance);
    }

    private static AgentSmithConfig BuildConfig(string repoUrl = "https://gitlab.com/org/my-api") =>
        new()
        {
            Projects = new Dictionary<string, ProjectConfig>
            {
                ["my-api"] = new() { Source = new SourceConfig { Url = repoUrl } }
            }
        };

    [Fact]
    public async Task HandleAsync_LabeledSecurityReview_ReturnsSecurityScan()
    {
        var sut = CreateHandler();
        var payload = """
        {
            "object_attributes": { "action": "update", "iid": 3 },
            "labels": [{ "title": "security-review" }],
            "project": { "path": "my-api", "web_url": "https://gitlab.com/org/my-api" }
        }
        """;

        var result = await sut.HandleAsync(payload, EmptyHeaders);

        result.Handled.Should().BeTrue();
        result.Pipeline.Should().Be("security-scan");
        result.ProjectName.Should().Be("my-api");
        result.TicketId.Should().Be("3");
    }

    [Fact]
    public async Task HandleAsync_NoMatchingLabel_ReturnsNotHandled()
    {
        var sut = CreateHandler();
        var payload = """
        {
            "object_attributes": { "action": "update", "iid": 3 },
            "labels": [{ "title": "needs-review" }],
            "project": { "path": "my-api", "web_url": "https://gitlab.com/org/my-api" }
        }
        """;

        var result = await sut.HandleAsync(payload, EmptyHeaders);

        result.Handled.Should().BeFalse();
    }

    [Fact]
    public void CanHandle_CorrectPlatform()
    {
        var sut = CreateHandler();

        sut.CanHandle("gitlab", "merge_request").Should().BeTrue();
        sut.CanHandle("gitlab", "push").Should().BeFalse();
    }
}
