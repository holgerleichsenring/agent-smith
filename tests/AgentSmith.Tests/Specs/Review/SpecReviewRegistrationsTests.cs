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

    [Fact]
    public void SpecCutReviserAndResidues_ResolveFromTheRealServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddProvider(NullLoggerProvider.Instance));
        ServerCompositionBuilder.ConfigureServices(services, "agentsmith.yml");
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<ISpecCutReviser>().Should().BeOfType<SpecCutReviser>();
        scope.ServiceProvider.GetRequiredService<SpecRevisionGuard>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<SpecCutResidues>().Should().NotBeNull();
    }

    [Fact]
    public void ReviewExhaustionAndPark_ResolveWithTheReviewStepFromTheRealServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddProvider(NullLoggerProvider.Instance));
        ServerCompositionBuilder.ConfigureServices(services, "agentsmith.yml");
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<ReviewExhaustion>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<ReviewObjectionPark>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<
            AgentSmith.Contracts.Commands.ICommandHandler<AgentSmith.Application.Models.ReviewSpecsContext>>()
            .Should().BeOfType<AgentSmith.Application.Services.Handlers.ReviewSpecsHandler>();
    }

    [Fact]
    public void ProposalReviewRounds_AndTheHeldApproval_ResolveFromTheRealServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddProvider(NullLoggerProvider.Instance));
        ServerCompositionBuilder.ConfigureServices(services, "agentsmith.yml");
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<ProposalReviewLoop>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<ProposalReProposalGuard>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<AgentSmith.Application.Services.SpecDialog.SpecDialogProposalReview>()
            .Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<AgentSmith.Server.Services.SpecDialog.SpecDialogOutcomeConfirmer>()
            .Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<AgentSmith.Server.Services.SpecDialog.SpecDialogUnreviewedApproval>()
            .Should().NotBeNull();
    }

    [Fact]
    public void AddSpecReview_ResolvesSeriesContractRound()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddProvider(NullLoggerProvider.Instance));
        ServerCompositionBuilder.ConfigureServices(services, "agentsmith.yml");
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<SeriesContractRound>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<ISeriesContractReviewer>().Should().BeOfType<SeriesContractReviewer>();
        scope.ServiceProvider.GetRequiredService<SeriesContractAdmission>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<ProposalSeriesRound>().Should().NotBeNull();
    }

    [Fact]
    public void AddSpecReview_ResolvesContractReaderAndCheck()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddProvider(NullLoggerProvider.Instance));
        ServerCompositionBuilder.ConfigureServices(services, "agentsmith.yml");
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<AgentSmith.Application.Services.Specs.SeriesContractReader>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<AgentSmith.Application.Services.Specs.SeriesContractCheck>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<AgentSmith.Application.Services.Specs.SpecDerivationParser>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<AgentSmith.Server.Services.SpecDialog.FiledSeriesFactory>().Should().NotBeNull();
    }
}
