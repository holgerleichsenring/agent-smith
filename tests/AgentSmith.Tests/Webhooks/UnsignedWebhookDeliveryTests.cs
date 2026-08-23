using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Core.Services.Webhooks;
using AgentSmith.Server.Services.Webhooks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Webhooks;

/// <summary>
/// p0506 at the route: the signature check is the whole gate in front of the handlers
/// (WebhookRequestProcessor verifies, then dispatches). The sharpest unauthenticated path
/// was never a new run — it was a forged PR-comment approval, which routes to the dialogue
/// router and publishes an ANSWER to a master blocked on a question. Both shapes must be
/// refused before any handler runs on a deployment that configured a secret.
/// </summary>
public sealed class UnsignedWebhookDeliveryTests
{
    [Fact]
    public async Task Webhook_UnsignedGithubIssues_ReachesNoHandler()
    {
        var handler = new SpyWebhookHandler();
        var processor = Processor(handler);

        var (status, _) = await processor.ProcessAsync(
            "/webhook/github", """{"action":"labeled"}""", Headers("issues"));

        status.Should().Be(401);
        handler.WasAsked.Should().BeFalse();
    }

    [Fact]
    public async Task Webhook_UnsignedPrCommentApproval_IsRefusedBeforeAnyHandlerRuns()
    {
        var handler = new SpyWebhookHandler();
        var processor = Processor(handler);

        var (status, _) = await processor.ProcessAsync(
            "/webhook/github", """{"action":"created","comment":{"body":"/approve"}}""",
            Headers("issue_comment"));

        status.Should().Be(401);
        handler.WasAsked.Should().BeFalse();
    }

    private static WebhookRequestProcessor Processor(IWebhookHandler handler)
    {
        var services = new ServiceCollection()
            .AddSingleton<IWebhookSecretResolver>(new WebhookSecretResolver(_ => "the-shared-secret"))
            .AddSingleton(new ServerContext("agentsmith.yml"))
            .AddSingleton<IConfigurationLoader>(new FixedConfigurationLoader(new AgentSmithConfig()))
            .AddSingleton<IWebhookHandler>(handler)
            .BuildServiceProvider();
        return new WebhookRequestProcessor(services, "agentsmith.yml", NullLogger.Instance);
    }

    private static Dictionary<string, string> Headers(string githubEvent) =>
        new(StringComparer.OrdinalIgnoreCase) { ["X-GitHub-Event"] = githubEvent };

    private sealed class SpyWebhookHandler : IWebhookHandler
    {
        public bool WasAsked { get; private set; }

        public bool CanHandle(string platform, string eventType)
        {
            WasAsked = true;
            return true;
        }

        public Task<WebhookResult> HandleAsync(
            string payload, IDictionary<string, string> headers,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(WebhookResult.HandledNoRoute());
    }

    private sealed class FixedConfigurationLoader(AgentSmithConfig config) : IConfigurationLoader
    {
        public ConfigFileReadFact? LastRead => null;

        public AgentSmithConfig LoadConfig(string configPath) => config;
    }
}
