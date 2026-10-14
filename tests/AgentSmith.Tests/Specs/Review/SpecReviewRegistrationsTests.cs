using AgentSmith.Application.Services.Specs.Review;
using AgentSmith.Server.Services;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Specs.Review;

/// <summary>2026-10-13-8762e: the spec review's services resolve from the server's own composition.</summary>
[Trait("TestProcess", "env-1")]
[Collection(TestSupport.EnvVarCollection.Name)]
public sealed class SpecReviewRegistrationsTests
{
    [Fact]
    public void SpecReviewRegistrations_ResolveFromTheRealServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddProvider(NullLoggerProvider.Instance));
        ServerCompositionBuilder.ConfigureServices(services, "agentsmith.yml");
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<SpecRoundEvidence>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<ISpecAttackReviewer>().Should().BeOfType<SpecAttackReviewer>();
        scope.ServiceProvider.GetRequiredService<SpecAttackAdmission>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<SpecReviewRoundPublisher>().Should().NotBeNull();
    }
}
