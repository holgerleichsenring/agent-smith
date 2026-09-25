using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Services.SpecDialog;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.TestSupport;

/// <summary>
/// 2026-09-25-8e51e: the amendment collaborator a test about FILING never reaches. A conversation
/// that approves a proposal takes the accept path; only an approval that picked the amendment
/// reaches this, so a test about the other door keeps saying what it is about.
/// </summary>
internal static class Amendments
{
    internal static TicketAmendment Unused() =>
        new(new AgentSmithConfig(),
            new Mock<IServiceScopeFactory>().Object,
            new FiledWorkTrackerProjects(new AgentSmithConfig()),
            new SpecDialogTicketTextRepository(Mock.Of<IUnitOfWork>()),
            ApprovedSetDoubles.Recorder(),
            new Mock<ITicketProviderFactory>().Object,
            new PhaseTicketRenderer(), new EpicChildOrderer(),
            ApprovedSetDoubles.Branch(),
            NullLogger<TicketAmendment>.Instance);
}
