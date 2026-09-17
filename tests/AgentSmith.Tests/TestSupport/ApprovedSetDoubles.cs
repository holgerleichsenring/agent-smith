using AgentSmith.Application.Services.Persistence;
using AgentSmith.Application.Services.Scope;
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

    internal static ApprovedPhaseSetRecorder Recorder(ISpecApprovalStore? store = null) =>
        new(store ?? Store(), TimeProvider.System, NullLogger<ApprovedPhaseSetRecorder>.Instance);
}
