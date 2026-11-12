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
    public void ProposalReviewPass_AndTheHeldApproval_ResolveFromTheRealServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddProvider(NullLoggerProvider.Instance));
        ServerCompositionBuilder.ConfigureServices(services, "agentsmith.yml");
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<ProposalAttackPass>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<AgentSmith.Application.Services.SpecDialog.ProposalReviewPass>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<AgentSmith.Application.Services.SpecDialog.SpecDialogProposalReview>()
            .Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<AgentSmith.Server.Services.SpecDialog.SpecDialogDecisionPath>()
            .Should().NotBeNull();
    }

    [Fact]
    public void ReviewProposalTool_FromTheRealServiceProvider_IsOnTheDesignPartnersSurface()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddProvider(NullLoggerProvider.Instance));
        ServerCompositionBuilder.ConfigureServices(services, "agentsmith.yml");
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var context = new AgentSmith.Application.Models.AgenticMasterContext("design-partner-master",
            new AgentSmith.Domain.Entities.Repository(new AgentSmith.Domain.Models.BranchName("main"), "/tmp"), string.Empty,
            new AgentSmith.Contracts.Models.Configuration.AgentConfig { Type = "claude" }, new AgentSmith.Contracts.Commands.PipelineContext());
        context.Pipeline.Set(AgentSmith.Contracts.Commands.ContextKeys.SpecDialogReplySlot, new AgentSmith.Contracts.Models.SpecDialogReplySlot());

        scope.ServiceProvider.GetRequiredService<AgentSmith.Application.Services.Tools.TicketActToolsFactory>().Tools(context)
            .Select(t => t.Name).Should().Contain(AgentSmith.Application.Services.Tools.ReviewProposalToolHost.Name);
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

    [Fact]
    public void SpecReviewRegistrations_ResolveTheRegroundServices()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddProvider(NullLoggerProvider.Instance));
        ServerCompositionBuilder.ConfigureServices(services, "agentsmith.yml");
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<SpecCitationHistory>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<SpecCitationRegrounder>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<ISpecEntryGrounding>().Should().BeOfType<SpecEntryGrounding>();
        scope.ServiceProvider.GetRequiredService<AgentSmith.Contracts.Commands.ICommandHandler<
            AgentSmith.Application.Models.SelectSpecContext>>().Should().NotBeNull("SelectSpec takes the entry grounding");
    }
}
