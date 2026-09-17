using AgentSmith.Application.Services.Persistence;
using AgentSmith.Application.Services.Scope;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Specs;
using AgentSmith.Server.Services.SpecDialog;
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

    internal static ApprovedPhaseSetRecorder Recorder(ISpecApprovalStore? store = null) =>
        new(store ?? Store(), TimeProvider.System, NullLogger<ApprovedPhaseSetRecorder>.Instance);

    /// <summary>
    /// 2026-09-17-0e79d: the epic filer stores the approved set under the WORK ticket it files,
    /// so it needs a recorder of its own. A test that reads the stored set passes its own store.
    /// </summary>
    internal static EpicTicketFiler EpicFiler(ISpecApprovalStore? store = null) =>
        new(new PhaseTicketRenderer(), new EpicChildOrderer(), Recorder(store),
            new EpicSliceRecordFiler(new PhaseTicketRenderer(), NullLogger<EpicSliceRecordFiler>.Instance),
            NullLogger<EpicTicketFiler>.Instance);
}
