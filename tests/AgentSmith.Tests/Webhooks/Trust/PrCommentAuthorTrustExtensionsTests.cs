using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Webhooks;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using AgentSmith.Infrastructure.Extensions;
using AgentSmith.Infrastructure.Services.Providers.Source;
using AgentSmith.Infrastructure.Services.Webhooks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace AgentSmith.Tests.Webhooks.Trust;

public sealed class PrCommentAuthorTrustExtensionsTests
{
    [Theory]
    [InlineData("github", typeof(GitHubAuthorAssociationTrust))]
    [InlineData("gitlab", typeof(GitLabMemberAccessTrust))]
    [InlineData("azuredevops", typeof(AzureDevOpsRepoContributeTrust))]
    public void AddPrCommentAuthorTrust_ResolvesOneTrustPerHost(string platform, Type expected)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHttpClient();
        services.AddSingleton(new SecretsProvider());
        services.AddSingleton(new ServerContext("config.yml"));
        services.AddSingleton(Mock.Of<IConfigurationLoader>());
        services.AddSingleton(Mock.Of<IAzDoClientFactory>());
        services.AddPrCommentAuthorTrust();

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true });

        provider.GetRequiredKeyedService<IPrCommentAuthorTrust>(platform).Should().BeOfType(expected);
    }
}
