using AgentSmith.Contracts.Services;
using AgentSmith.Server.Services;
using AgentSmith.Server.Services.Webhooks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Server;

/// <summary>The server composition wires the record nudge and the three pull-request state handlers.</summary>
[Trait("TestProcess", "env-1")]
[Collection(TestSupport.EnvVarCollection.Name)]
public sealed class PrRecordTriggerCompositionTests
{
    [Fact]
    public void PrRecordTriggerComposition_Server_ResolvesNudgeAndStateHandlers()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddProvider(NullLoggerProvider.Instance));
        ServerCompositionBuilder.ConfigureServices(services, "agentsmith.yml");

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<PrRecordNudge>().Should().NotBeNull();
        provider.GetRequiredService<PrCommentCommandAdmission>().Should().NotBeNull();
        provider.GetServices<IWebhookHandler>().Select(h => h.GetType()).Should().Contain(
            [typeof(GitHubPrStateWebhookHandler), typeof(GitLabMrStateWebhookHandler), typeof(AzureDevOpsPrStateWebhookHandler),
             typeof(GitHubPrReviewWebhookHandler), typeof(AzureDevOpsPrReviewVoteWebhookHandler), typeof(GitLabMrReviewWebhookHandler)]);
    }
}
