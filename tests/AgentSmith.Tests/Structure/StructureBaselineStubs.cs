using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Application.Services.Structure;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Structure;

/// <summary>
/// 2026-10-06-8565c: a baseline service over real collaborators. A round whose principles carry
/// no Limits takes none, which is what every pre-existing round test composes.
/// </summary>
internal static class StructureBaselineStubs
{
    public static StructureBaseline Real(IStructureEngine? engine = null) => new(
        new StructureSourceListing(), engine ?? StructureEngineFixture.Engine(), new StructureLimits(),
        new StructureJudge(), new SandboxFileReaderFactory(), NullLogger<StructureBaseline>.Instance);
}
