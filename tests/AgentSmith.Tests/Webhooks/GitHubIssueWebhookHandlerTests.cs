using AgentSmith.Cli.Services.Webhooks;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Webhooks;

public sealed class GitHubIssueWebhookHandlerTests
{
    private const string ConfigPath = "test-config.yml";

    private static readonly IDictionary<string, string> EmptyHeaders =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    private static GitHubIssueWebhookHandler CreateHandler(AgentSmithConfig? config = null)
    {
        var loader = new Mock<IConfigurationLoader>();
        loader.Setup(c => c.LoadConfig(ConfigPath)).Returns(config ?? BuildConfig());
        return new GitHubIssueWebhookHandler(loader.Object, new ServerContext(ConfigPath),
            NullLogger<GitHubIssueWebhookHandler>.Instance);
    }

    private static AgentSmithConfig BuildConfig(string repoUrl = "https://github.com/org/my-api") =>
        new()
        {
            Projects = new Dictionary<string, ProjectConfig>
            {
                ["my-api"] = new() { Source = new SourceConfig { Url = repoUrl } }
            }
        };

    [Fact]
    public async Task HandleAsync_LabeledAgentSmith_ReturnsHandled()
    {
        var sut = CreateHandler();
        var payload = """
        {
            "action": "labeled",
            "label": { "name": "agent-smith" },
            "issue": { "number": 42 },
            "repository": { "name": "my-api", "html_url": "https://github.com/org/my-api" }
        }
        """;

        var result = await sut.HandleAsync(payload, EmptyHeaders);

        result.Handled.Should().BeTrue();
        result.ProjectName.Should().Be("my-api");
        result.TicketId.Should().Be("42");
        result.TriggerInput.Should().BeNull();
    }

    [Fact]
    public async Task HandleAsync_WrongLabel_ReturnsNotHandled()
    {
        var sut = CreateHandler();
        var payload = """
        {
            "action": "labeled",
            "label": { "name": "bug" },
            "issue": { "number": 42 },
            "repository": { "name": "my-api", "html_url": "https://github.com/org/my-api" }
        }
        """;

        var result = await sut.HandleAsync(payload, EmptyHeaders);

        result.Handled.Should().BeFalse();
    }

    [Fact]
    public async Task HandleAsync_NotLabeled_ReturnsNotHandled()
    {
        var sut = CreateHandler();
        var payload = """{ "action": "opened", "issue": { "number": 1 }, "repository": { "name": "x", "html_url": "https://github.com/org/x" } }""";

        var result = await sut.HandleAsync(payload, EmptyHeaders);

        result.Handled.Should().BeFalse();
    }

    [Fact]
    public void CanHandle_CorrectPlatform()
    {
        var sut = CreateHandler();

        sut.CanHandle("github", "issues").Should().BeTrue();
        sut.CanHandle("github", "pull_request").Should().BeFalse();
        sut.CanHandle("gitlab", "issues").Should().BeFalse();
    }
}
