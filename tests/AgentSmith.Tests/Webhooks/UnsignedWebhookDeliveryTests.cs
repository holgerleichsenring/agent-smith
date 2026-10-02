using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Core.Services.Webhooks;
using AgentSmith.Infrastructure.Persistence.Services;
using AgentSmith.Server.Services.Webhooks;
using AgentSmith.Tests.TestSupport;
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

    [Fact]
    public async Task WebhookRequestProcessor_SignatureFailure_StillRecordsLastSeen()
    {
        // 2026-10-02-5ab2e: a delivery with a wrong secret proves the route works and the
        // secret does not — diagnostics must see that it ARRIVED.
        using var store = new ServerStateStore();
        var tracker = new DbWebhookDeliveryTracker(store.ScopeFactory, NullLogger<DbWebhookDeliveryTracker>.Instance);

        var (status, _) = await Processor(new SpyWebhookHandler(), tracker).ProcessAsync(
            "/webhook/github", """{"action":"labeled"}""", Headers("issues"));

        status.Should().Be(401);
        (await tracker.GetLastSeenAsync()).Should().ContainKey("github");
    }

    private static WebhookRequestProcessor Processor(IWebhookHandler handler, IWebhookDeliveryTracker? tracker = null)
    {
        var services = new ServiceCollection();
        if (tracker is not null) services.AddSingleton(tracker);
        services
            .AddSingleton<IWebhookSecretResolver>(new WebhookSecretResolver(_ => "the-shared-secret"))
            .AddSingleton(new ServerContext("agentsmith.yml"))
            .AddSingleton<IConfigurationLoader>(new FixedConfigurationLoader(new AgentSmithConfig()))
            .AddSingleton<IWebhookHandler>(handler);
        return new WebhookRequestProcessor(services.BuildServiceProvider(), "agentsmith.yml", NullLogger.Instance);
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
