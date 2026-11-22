using System.Net;
using System.Text;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Services.Factories.ChatClientBuilders;
using AgentSmith.Infrastructure.Services.RateLimiting;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.ModelQuota;

/// <summary>
/// 2026-11-22-66e4a: Azure and OpenAI state on every response what is left of the deployment's minute. Through the
/// builder's test transport (p0239c, the seam p0493 measured the SDK retries with), a successful answer's
/// remaining-quota headers reach the throttle and cap it — the one count that also sees other callers.
/// </summary>
public sealed class ModelQuotaHeaderTests : IDisposable
{
    private const string KeySecret = "AS_QUOTAHEADERS_KEY";

    public ModelQuotaHeaderTests() => Environment.SetEnvironmentVariable(KeySecret, "test-key");
    public void Dispose() => Environment.SetEnvironmentVariable(KeySecret, null);

    [Fact]
    public async Task AzureSuccess_RemainingHeaders_CapTheDeploymentsBudget()
    {
        var headers = new ProviderQuotaHeaders();
        var agent = new AgentConfig
        {
            Type = "azure_openai", ApiKeySecret = KeySecret, Endpoint = "https://test.openai.azure.com", Deployment = "dep",
        };
        var bare = new OpenAiChatClientBuilder(AgentSmith.Tests.TestSupport.TestCredentials.EnvironmentOnly, new QuotaHandler(), headers)
            .Build(agent, new ModelAssignment { Model = "gpt-4.1" });
        var limiter = new LlmRateLimiter(new LlmRateLimitOptions(250, 250_000, LlmRateBudget.RuleFor("azure_openai")));
        var client = new RateLimitingChatClient(bare, limiter, "dep", new RetryConfig(), new ThrottleWaitReporter(),
            NullLogger<RateLimitingChatClient>.Instance, headers);

        await client.GetResponseAsync([new ChatMessage(ChatRole.User, "hi")]);

        var snapshot = limiter.Snapshot(new ModelQuotaScope("k", "azure_openai", "dep"));
        snapshot.ProviderRemainingTokens.Should().Be(1_000);
        snapshot.FreeTokens.Should().BeLessThan(20_000, "the provider said 1000 tokens are left, plus what refilled since");
        snapshot.FreeRequests.Should().BeLessThan(10);
    }

    private sealed class QuotaHandler : HttpMessageHandler
    {
        private const string Body = """
            {"id":"c","object":"chat.completion","created":1,"model":"m",
             "choices":[{"index":0,"message":{"role":"assistant","content":"ok"},"finish_reason":"stop"}],
             "usage":{"prompt_tokens":10,"completion_tokens":1,"total_tokens":11}}
            """;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(Body, Encoding.UTF8, "application/json"),
            };
            response.Headers.Add("x-ratelimit-remaining-tokens", "1000");
            response.Headers.Add("x-ratelimit-remaining-requests", "3");
            return Task.FromResult(response);
        }
    }
}
