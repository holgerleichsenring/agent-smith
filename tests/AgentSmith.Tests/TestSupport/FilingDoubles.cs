using AgentSmith.Application.Services.Claim;
using AgentSmith.Application.Services.Filing;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Persistence;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Specs;
using AgentSmith.Server.Services.SpecDialog;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.TestSupport;

/// <summary>
/// 2026-11-17-5701b: the 'file' run's steps over doubles — the real planner, filers, start pass and handlers; the
/// tracker, the stores and the branch remote are the test's. Every collaborator not passed is the empty one a test
/// that does not look at it wants: nothing approved, nothing required, nothing routed.
/// </summary>
internal static class FilingDoubles
{
    internal sealed record Parts
    {
        public ISpecApprovalStore? Store { get; init; }
        public FiledWorkStarter? Starter { get; init; }
        public RecordingBranchSources? Sources { get; init; }
        public ISpecSetPointerStore? Pointers { get; init; }
        public IConversationMockReader? Mocks { get; init; }
        public IReferenceSetReader? References { get; init; }
        public InMemoryTicketFacts? Facts { get; init; }
        public ITicketRequirementStore? Requirements { get; init; }
        public ITicketExistence? Existence { get; init; }
        public TimeProvider? Time { get; init; }
        public SeriesConfig? Series { get; init; }
    }

    internal static FilingSteps Steps(AgentSmithConfig config, ITicketProviderFactory factory, Parts? parts = null)
    {
        var p = parts ?? new Parts();
        var store = p.Store ?? ApprovedSetDoubles.Store();
        var facts = p.Facts ?? new InMemoryTicketFacts();
        var planner = Planner(factory, p.Existence, p.Series);
        var tickets = new PlannedTicketFiler(ApprovedSetDoubles.Kinds(), p.Requirements ?? facts,
            ApprovedSetDoubles.SetFiler(store, p.Sources, p.Pointers, p.Mocks, p.References), p.Time ?? TimeProvider.System);
        var starts = new TicketGraphStarts(new UnmetTicketRequirements(p.Requirements ?? facts, facts, store),
            p.Starter ?? FiledWorkDoubles.Starter(), new TicketGraphLinks(factory, p.Time ?? TimeProvider.System, new TicketsConfig(),
                NullLogger<TicketGraphLinks>.Instance), NullLogger<TicketGraphStarts>.Instance);
        return new FilingSteps(config,
            new PlanFilingHandler(planner, NullLogger<PlanFilingHandler>.Instance),
            new FileTicketsHandler(new FilingPlanFiler(factory, tickets, starts, NullLogger<FilingPlanFiler>.Instance)));
    }

    /// <summary>The real planner; series.max_specs from <paramref name="settings"/>, the REAL existence read by default.</summary>
    internal static FilingPlanner Planner(ITicketProviderFactory factory, ITicketExistence? existence = null, SeriesConfig? settings = null)
    {
        var series = new SeriesFilingPlan(new SpecTicketRenderer(), new EpicChildOrderer(), new FiledSeriesFactory(
            ApprovedSetDoubles.SeriesIds(), ApprovedSetDoubles.DraftIds(), SeriesSettingsDoubles.Reader(settings),
            NullLogger<FiledSeriesFactory>.Instance));
        return new FilingPlanner(series, new ListFilingPlan(existence ?? Existence(factory), new TicketGraphOrderer(), series, new SeriesTicketRegion(new SpecTicketRenderer())),
            new BugTicketRenderer());
    }

    // Every ticket a list requires exists unless a test says otherwise.
    private static ITicketExistence Existence(ITicketProviderFactory factory) =>
        new TicketExistence(factory, NullLogger<TicketExistence>.Instance);
}
