using AgentSmith.Application.Services.Specs.Review;
using AgentSmith.Server.Services;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using AgentSmith.Application.Services.Filing;

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

        scope.ServiceProvider.GetRequiredService<AgentSmith.Application.Services.Specs.Revision.MasterSpecRevision>()
            .Should().NotBeNull("2026-11-19-3e10: revise_spec resolves without a review panel");
    }

    [Fact]
    public void ReviewSpecsStep_IsNotRegistered()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddProvider(NullLoggerProvider.Instance));
        ServerCompositionBuilder.ConfigureServices(services, "agentsmith.yml");

        services.Should().NotContain(d => d.ServiceType.Name.Contains("ReviewSpecsContext"),
            "2026-11-18-9c1cb: nothing reviews a spec before its master");
    }

    [Fact]
    public void TurnFlow_AndTheHeldApproval_ResolveFromTheRealServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddProvider(NullLoggerProvider.Instance));
        ServerCompositionBuilder.ConfigureServices(services, "agentsmith.yml");
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<AgentSmith.Application.Services.SpecDialog.SpecDialogTurnFlow>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<AgentSmith.Application.Services.SpecDialog.ProposalMechanicalChecks>()
            .Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<AgentSmith.Server.Services.SpecDialog.SpecDialogDecisionPath>()
            .Should().NotBeNull();
    }

    [Fact]
    public void DesignPartnersSurface_FromTheRealServiceProvider_OffersNoReviewProposal()
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
            .Select(t => t.Name).Should().NotContain("review_proposal",
                "2026-11-18-9c1ca: a master that wants a review spawns a sub-agent with a review brief")
            .And.Contain("state_work_picture", "the surface was composed");
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
        scope.ServiceProvider.GetRequiredService<AgentSmith.Application.Services.Filing.FiledSeriesFactory>().Should().NotBeNull();
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
