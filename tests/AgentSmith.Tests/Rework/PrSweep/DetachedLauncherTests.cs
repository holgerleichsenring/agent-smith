using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Core.Services.Webhooks;
using AgentSmith.Server.Contracts;
using AgentSmith.Server.Services.Webhooks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Rework.PrSweep;

/// <summary>2026-10-08-10b0: a PR route result starts its run with the named project and pipeline.</summary>
public sealed class DetachedLauncherTests
{
    private static IConfigurationLoader Loader()
    {
        var loader = new Mock<IConfigurationLoader>();
        loader.Setup(l => l.LoadConfig(It.IsAny<string>())).Returns(new AgentSmithConfig());
        return loader.Object;
    }

    [Fact]
    public async Task DetachedLauncher_PrReview_NoIntentParseNoLease()
    {
        var launcher = new Mock<IDetachedPipelineLauncher>();
        var context = new Dictionary<string, object> { ["PrNumber"] = "4" };
        var handler = new Mock<IWebhookHandler>();
        handler.Setup(h => h.CanHandle("github", "pull_request")).Returns(true);
        handler.Setup(h => h.HandleAsync(It.IsAny<string>(), It.IsAny<IDictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WebhookResult(true, "pr-review app pr:o/r#4", "pr-review", InitialContext: context, ProjectName: "app"));
        var services = new ServiceCollection()
            .AddSingleton<IWebhookSecretResolver>(new WebhookSecretResolver(_ => null))
            .AddSingleton(new ServerContext("agentsmith.yml"))
            .AddSingleton(Loader())
            .AddSingleton(handler.Object)
            .AddSingleton(launcher.Object);
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["X-GitHub-Event"] = "pull_request" };

        var (status, _) = await new WebhookRequestProcessor(services.BuildServiceProvider(), "agentsmith.yml", NullLogger.Instance)
            .ProcessAsync("/webhook/github", "{}", headers);

        status.Should().Be(202);
        launcher.Verify(l => l.LaunchAsync("app", "pr-review", context), Times.Once, "no model parses the trigger input");
        DetachedPipelineLauncher.RequestFor("app", "pr-review", context).TicketId.Should().BeNull("a ticketless run takes no lease");
    }
}
