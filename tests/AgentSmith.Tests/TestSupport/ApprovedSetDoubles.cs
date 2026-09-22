using AgentSmith.Application.Services.Persistence;
using AgentSmith.Application.Services.Scope;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Application.Services.Tickets;
using AgentSmith.Contracts.Specs;
using AgentSmith.Server.Services.SpecDialog;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.TestSupport;

/// <summary>
/// 2026-09-17-0e79a: the approved-record collaborators over an EMPTY in-memory store — what
/// every pre-existing test wants, because a ticket nobody approved must behave exactly as it
/// did. A test that needs a record seeds its own store and passes it in.
/// </summary>
internal static class ApprovedSetDoubles
{
    internal static ISpecApprovalStore Store() => new InMemorySpecApprovalStore();

    internal static ApprovedSpecSetCarrier Carrier(ISpecApprovalStore? store = null) =>
        new(store ?? Store(), NullLogger<ApprovedSpecSetCarrier>.Instance);

    internal static ApprovedSpecSetResolver Resolver(ISpecApprovalStore? store = null) =>
        new(store ?? Store(), NullLogger<ApprovedSpecSetResolver>.Instance);

    internal static ApprovedRepoScope Scope(ISpecApprovalStore? store = null) =>
        new(Resolver(store), NullLogger<ApprovedRepoScope>.Instance);

    /// <summary>2026-09-17-0e79b: the kept-set notice over doubles that record nothing — what a
    /// test wants when nothing was approved, so no notice can be posted at all.</summary>
    internal static ApprovedSetKeptNotice KeptNotice() => new(
        new RecordingTicketComments().Factory(), new RecordingRunDecisions(),
        NullLogger<ApprovedSetKeptNotice>.Instance);

    /// <summary>
    /// 2026-09-22-b3d7: the ONE path a phase and an approved cut are both filed through — create,
    /// store, start. A test that reads the stored set, or that watches the starter, passes its own.
    /// </summary>
    internal static ApprovedSetTicketFiler SetFiler(
        ISpecApprovalStore? store = null, FiledWorkStarter? starter = null) =>
        new(Recorder(store), starter ?? FiledWorkDoubles.Starter(), Kinds(),
            NullLogger<ApprovedSetTicketFiler>.Instance);

    internal static ApprovedPhaseSetRecorder Recorder(ISpecApprovalStore? store = null) =>
        new(store ?? Store(), TimeProvider.System, NullLogger<ApprovedPhaseSetRecorder>.Instance);

    /// <summary>2026-09-18-b4f0: the real resolver — a tracker that configures no kinds
    /// resolves none, which is what every test that does not set one expects.</summary>
    internal static TicketKindResolver Kinds(ILogger<TicketKindResolver>? logger = null) =>
        new(logger ?? NullLogger<TicketKindResolver>.Instance);
}
