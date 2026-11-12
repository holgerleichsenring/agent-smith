using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Loop;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Server.Services;
using AgentSmith.Server.Services.SpecDialog;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using static AgentSmith.Tests.SpecDialog.ProposalReview.TicketPlanReviewRig;

namespace AgentSmith.Tests.SpecDialog.ProposalReview;

/// <summary>2026-10-14-dbcek: the plan review and the existence port resolve from the server's own composition.</summary>
[Trait("TestProcess", "env-1")]
[Collection(TestSupport.EnvVarCollection.Name)]
public sealed class TicketPlanCompositionTests
{
    [Fact]
    public async Task SpecDialogProposalReview_FromRealServiceCollection_ReviewsATicketList()
    {
        var rig = new TicketPlanReviewRig();
        rig.ReviewerAnswers.Enqueue(Finding("order-ui", "order"));
        var services = Composition();
        services.AddSingleton(rig.Factory());
        // 2026-11-10-8e59: there is no config here; an unreadable block reviews nothing, so the test supplies one.
        services.AddSingleton(ProposalReviewRig.Reader(() => ProposalReviewRig.Of(rounds: 4)));
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var review = scope.ServiceProvider.GetRequiredService<SpecDialogProposalReview>();
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.SpecDialogOutcome, (OutcomeProposal)List(Ticket("order-api"), Ticket("order-ui", "order-api")));
        await review.ReviewAsync(pipeline, new AgentConfig(), PipelineCostTracker.GetOrCreate(pipeline), CancellationToken.None);

        rig.ReviewerPrompts.Should().ContainSingle().Which.Should().Contain("Review the list as a PLAN");
        pipeline.Get<OutcomeProposal>(ContextKeys.SpecDialogOutcome).Findings.Should().ContainSingle()
            .Which.SpecId.Should().Be("order-ui");
    }

    [Fact]
    public void TicketExistence_ServerComposition_ResolvesThePort()
    {
        using var provider = Composition().BuildServiceProvider();
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<ITicketExistence>().Should().BeOfType<TicketExistence>();
        scope.ServiceProvider.GetRequiredService<TicketPlanReview>().Should().NotBeNull();
    }

    private static ServiceCollection Composition()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddProvider(NullLoggerProvider.Instance));
        ServerCompositionBuilder.ConfigureServices(services, "agentsmith.yml");
        return services;
    }
}
