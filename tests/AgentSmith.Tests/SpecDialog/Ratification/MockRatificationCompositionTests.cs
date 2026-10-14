using AgentSmith.Application.Services.Specs;
using AgentSmith.Server.Services;
using AgentSmith.Server.Services.SpecDialog;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.SpecDialog.Ratification;

/// <summary>2026-10-13-8762s: the server composition wires the ratification, the filing stamp, the review's
/// offer, and hands the branch write the conversation's mock reader.</summary>
[Trait("TestProcess", "env-1")]
[Collection(TestSupport.EnvVarCollection.Name)]
public sealed class MockRatificationCompositionTests
{
    [Fact]
    public void MockRatificationComposition_Server_ResolvesRatificationStampReviewAndBranchReader()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddProvider(NullLoggerProvider.Instance));
        ServerCompositionBuilder.ConfigureServices(services, "agentsmith.yml");

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<MockRatification>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<RatifiedMockStamp>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<RatifiedMockReview>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<FiledSpecBranch>().Should().NotBeNull();
        scope.ServiceProvider.GetService<AgentSmith.Contracts.Sandbox.IConversationMockReader>().Should()
            .NotBeNull("the branch write's optional reader is filled from the server's conversation store");
    }
}
