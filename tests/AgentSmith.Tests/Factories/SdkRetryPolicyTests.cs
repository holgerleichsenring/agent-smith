using System.ClientModel;
using System.Net;
using System.Text;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Infrastructure.Services.Factories.ChatClientBuilders;
using FluentAssertions;
using Microsoft.Extensions.AI;

namespace AgentSmith.Tests.Factories;

/// <summary>
/// p0493: the Azure and OpenAI SDK retries a 429 by itself, and that layer is invisible to
/// us. Measured through the p0239c test-transport seam before anything was changed: a 429
/// cost FOUR attempts inside the SDK; Retry-After was honoured exactly and with no ceiling
/// (20s asked → 20s, 20s, 20s); with no header the three retries fired in 0.03 seconds.
/// Stacked under TransientRetryChatClient's five attempts that is up to 24 provider calls for
/// one logical call, none of it logged, none of it re-acquiring throttle capacity, and an
/// hour-long Retry-After would park a run for three hours where no bound of ours could reach
/// it. So the retry lives in exactly one layer now, and these pin which one.
/// </summary>
public sealed class SdkRetryPolicyTests : IDisposable
{
    private const string KeySecret = "AS_RETRYPOLICY_KEY";

    public SdkRetryPolicyTests() => Environment.SetEnvironmentVariable(KeySecret, "test-key");
    public void Dispose() => Environment.SetEnvironmentVariable(KeySecret, null);

    [Fact]
    public async Task OpenAi_TheSdkRetryPolicy_IsDisabledSoARefusalReachesOurLayer()
    {
        var handler = new RateLimitHandler();
        var client = new OpenAiChatClientBuilder(handler).Build(
            new AgentConfig { Type = "openai", ApiKeySecret = KeySecret },
            new ModelAssignment { Model = "gpt-4.1" });

        await Refuses(client);

        handler.Attempts.Should().Be(1,
            "a 429 the SDK retries three more times is three more requests at an endpoint that "
            + "just said too many, and no ceiling of ours can reach the wait");
    }

    [Fact]
    public async Task Azure_TheSdkRetryPolicy_IsDisabledSoARefusalReachesOurLayer()
    {
        var handler = new RateLimitHandler();
        var agent = new AgentConfig
        {
            Type = "azure_openai", ApiKeySecret = KeySecret,
            Endpoint = "https://test.openai.azure.com", Deployment = "my-deploy",
        };
        var client = new OpenAiChatClientBuilder(handler).Build(agent, new ModelAssignment { Model = "gpt-4.1" });

        await Refuses(client);

        handler.Attempts.Should().Be(1, "the live 429 came from an Azure deployment");
    }

    /// <summary>The refusal must also arrive as the type our retry layer recognises — a 429
    /// the outer loop cannot classify is a run that dies on the first rate limit.</summary>
    private static async Task Refuses(IChatClient client) =>
        (await FluentActions.Awaiting(() => client.GetResponseAsync([new ChatMessage(ChatRole.User, "hi")]))
            .Should().ThrowAsync<ClientResultException>()).Which.Status.Should().Be(429);

    private sealed class RateLimitHandler : HttpMessageHandler
    {
        public int Attempts { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Attempts++;
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests)
            {
                Content = new StringContent(
                    "{\"error\":{\"code\":\"429\",\"message\":\"rate limit exceeded\"}}",
                    Encoding.UTF8, "application/json"),
            };
            response.Headers.Add("Retry-After", "1");
            return Task.FromResult(response);
        }
    }
}
