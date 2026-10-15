using AgentSmith.Application.Services.Persistence;
using AgentSmith.Application.Services.Rework;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Tickets;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Server.Services.SpecDialog.Continuation;
using AgentSmith.Server.Services.Tickets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.TestSupport;

/// <summary>
/// 2026-10-13-9a59l: the continuation collaborator a test about an UNBOUND conversation never reaches —
/// only a spec or epic approved in a ticket's own conversation is routed to it.
/// </summary>
internal static class Continuations
{
    internal static TicketContinuationAct Unused(OutcomeTicketFiler filer)
    {
        var config = new AgentSmithConfig();
        var trackers = new Mock<ITicketProviderFactory>().Object;
        var scopes = new Mock<IServiceScopeFactory>().Object;
        var reader = new TicketContinuationStateReader(config, new FiledWorkTrackerProjects(config), trackers,
            new TicketClosure(new TrackerIdentity(trackers, NullLogger<TrackerIdentity>.Instance), NullLogger<TicketClosure>.Instance),
            new TicketRunsQuery(scopes), new TicketAttempts(), new TicketSeriesRecordsReader(scopes),
            new InMemorySpecSetPointerStore(), ApprovedSetDoubles.Recorder(),
            new OperatorStartArm(Mock.Of<IActiveRunLease>(), Mock.Of<IPreviousAttemptReader>(), Mock.Of<IRunCheckpointStore>()),
            NullLogger<TicketContinuationStateReader>.Instance);
        return new TicketContinuationAct(config, new SpecDialogTicketTextRepository(Mock.Of<IUnitOfWork>()), reader,
            Amendments.Unused(), new ClosedTicketFollowUp(filer, new FollowUpLink(trackers, NullLogger<FollowUpLink>.Instance)),
            ApprovedSetDoubles.Recorder(), Mock.Of<ITicketActivityNudge>(),
            new OperatorStart(Mock.Of<IReworkNudges>(), trackers, FiledWorkDoubles.Move(), NullLogger<OperatorStart>.Instance,
                TicketRequirementDoubles.None()),
            SeriesSettingsDoubles.Reader(), NullLogger<TicketContinuationAct>.Instance);
    }
}
