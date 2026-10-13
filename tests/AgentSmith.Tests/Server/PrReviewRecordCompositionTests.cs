using AgentSmith.Contracts.Reviews;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Services;
using AgentSmith.Server.Services.Rework;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Server;

/// <summary>2026-10-13-9a59b: the server composition wires the record refresh, its writer and the read port.</summary>
[Trait("TestProcess", "env-1")]
[Collection(TestSupport.EnvVarCollection.Name)]
public sealed class PrReviewRecordCompositionTests
{
    [Fact]
    public void PrReviewRecordComposition_Server_ResolvesRefreshWriterAndReadPort()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddProvider(NullLoggerProvider.Instance));
        ServerCompositionBuilder.ConfigureServices(services, "agentsmith.yml");

        services.Should().Contain(d => d.ServiceType == typeof(PrReviewRecordRefresh));
        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<PrReviewRecordRefresh>().Should().NotBeNull();
        using var scope = provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<IPrReviewRecordReader>().Should().BeOfType<PrReviewRecordRepository>();
        scope.ServiceProvider.GetRequiredService<PrReviewRecordWriter>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<NamedPullRequestReader>().Should().NotBeNull();
        provider.GetRequiredService<PrReviewEntryBuilder>().Should().NotBeNull();
    }
}
