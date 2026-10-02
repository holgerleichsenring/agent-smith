using AgentSmith.Contracts.Providers;
using AgentSmith.Infrastructure.Services.Providers.Source;
using AgentSmith.Infrastructure.Services.Providers.Tickets;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Credentials;

/// <summary>
/// 2026-10-02-5f89a: a pooled Azure DevOps connection belongs to an organization AND a token, so
/// a second connection to one org, or a re-pointed secret, never borrows the first token.
/// </summary>
public sealed class AzureDevOpsConnectionKeyingTests
{
    private static string Org() => $"https://dev.azure.com/org-{Guid.NewGuid():N}";

    [Fact]
    public void DefaultAzDoClientFactory_TwoTokensForOneOrg_BuildTwoConnections()
    {
        var org = Org();

        var first = DefaultAzDoClientFactory.Connect(org, "pat-a");
        var second = DefaultAzDoClientFactory.Connect(org, "pat-b");

        second.Should().NotBeSameAs(first);
        DefaultAzDoClientFactory.Connect(org, "pat-a").Should().BeSameAs(first);
    }

    [Fact]
    public void AzureDevOpsConnectionCache_RepointedToken_BuildsANewConnection()
    {
        var org = Org();
        var before = Cache(org, "old-pat").Connection();

        var after = Cache(org, "new-pat").Connection();

        after.Should().NotBeSameAs(before);
        Cache(org, "old-pat").Connection().Should().BeSameAs(before);
    }

    [Fact]
    public void AzureDevOpsConnectionKey_For_HoldsTheOrgButNotTheToken()
    {
        var key = AzureDevOpsConnectionKey.For("https://dev.azure.com/org", "plain-token");

        key.Should().StartWith("https://dev.azure.com/org|").And.NotContain("plain-token");
        AzureDevOpsConnectionKey.For("https://dev.azure.com/org", "other").Should().NotBe(key);
    }

    private static AzureDevOpsConnectionCache Cache(string org, string pat) =>
        new(new AzureDevOpsTicketConnection(org, "p", pat), NullLogger.Instance);
}
