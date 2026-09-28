using AgentSmith.Infrastructure.Services.Webhooks;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Server.Services.Webhooks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Webhooks;

public sealed class GitLabMrLabelWebhookHandlerTests
{
    private const string ConfigPath = "test-config.yml";

    private static readonly IDictionary<string, string> EmptyHeaders =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    private static Mock<IConfigurationLoader> Loader()
    {
        var loader = new Mock<IConfigurationLoader>();
        loader.Setup(c => c.LoadConfig(ConfigPath)).Returns(new AgentSmithConfig
        {
            Projects = new Dictionary<string, ResolvedProject>
            {
                ["my-api"] = new()
                {
                    Repos = new[] { new RepoConnection { Name = "api-repo", Url = "https://gitlab.com/org/my-api" } },
                },
            },
        });
        return loader;
    }

    private static GitLabMrLabelWebhookHandler CreateHandler() =>
        new(Loader().Object, new ServerContext(ConfigPath), new PrTriggerLabelResolver(),
            new PrReviewRouteResolver(new ConfiguredRepoFinder()), new PrRunContextFactory(),
            NullLogger<GitLabMrLabelWebhookHandler>.Instance);

    private static GitLabMrEventWebhookHandler CreateEventHandler() =>
        new(Loader().Object, new ServerContext(ConfigPath), new PrReviewRouteResolver(new ConfiguredRepoFinder()),
            new PrRunContextFactory(), NullLogger<GitLabMrEventWebhookHandler>.Instance);

    private static string Payload(string previous, string current, string extraAttrs = "", bool withChanges = true) => $$"""
        {
            "user": { "username": "dev-a" },
            "object_attributes": {
                "action": "update", "iid": 3, "source_branch": "feature/x",
                "last_commit": { "id": "headsha123" }{{extraAttrs}}
            },
            "labels": [{ "title": "security-review" }],
            {{(withChanges ? $$"""
            "changes": { "labels": { "previous": [{{previous}}], "current": [{{current}}] } },
            """ : "")}}
            "project": { "path_with_namespace": "org/my-api", "web_url": "https://gitlab.com/org/my-api" }
        }
        """;

    private const string SecurityReview = """{ "title": "security-review" }""";

    [Fact]
    public async Task GitLabMrLabel_Labeled_SeedsMrContextNotTicketId()
    {
        var result = await CreateHandler().HandleAsync(Payload("", SecurityReview), EmptyHeaders);

        result.Handled.Should().BeTrue();
        result.Pipeline.Should().Be("security-scan");
        result.ProjectName.Should().Be("my-api");
        result.TicketId.Should().BeNull();
        result.TriggerInput.Should().Be("security-scan my-api pr:org/my-api#3");
        result.InitialContext![ContextKeys.PrNumber].Should().Be("3");
        result.InitialContext[ContextKeys.CheckoutBranch].Should().Be("feature/x");
        result.InitialContext[ContextKeys.PrHead].Should().Be("headsha123");
        result.InitialContext[ContextKeys.SourceOverrideRepo].Should().Be("api-repo");
    }

    [Fact]
    public async Task GitLabMrLabel_LabelAlreadyPresent_IsNotHandled()
    {
        var result = await CreateHandler().HandleAsync(
            Payload(SecurityReview, SecurityReview + """, { "title": "backend" }"""), EmptyHeaders);

        result.Handled.Should().BeFalse();
    }

    [Fact]
    public async Task GitLabMrLabel_SourcePushOnLabelledMr_FallsThroughToPrReview()
    {
        var push = Payload("", "", extraAttrs: """, "oldrev": "prevsha" """, withChanges: false);
        IWebhookHandler[] registrationOrder = [CreateHandler(), CreateEventHandler()];

        WebhookResult? claimed = null;
        foreach (var handler in registrationOrder)
        {
            var result = await handler.HandleAsync(push, EmptyHeaders);
            if (result.Handled) { claimed = result; break; }
        }

        claimed.Should().NotBeNull();
        claimed!.Pipeline.Should().Be("pr-review");
    }

    [Fact]
    public async Task HandleAsync_NoMatchingLabelAdded_ReturnsNotHandled()
    {
        var result = await CreateHandler().HandleAsync(
            Payload("", """{ "title": "needs-review" }"""), EmptyHeaders);

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
