using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Application.Services.Structure;

namespace AgentSmith.Tests.Structure;

/// <summary>
/// 2026-10-06-8565c: a baseline service over real collaborators. A round whose principles carry
/// no Limits skips it, which is what every pre-existing round test composes.
/// </summary>
internal static class StructureBaselineStubs
{
    public static StructureBaseline Real() => new(
        new StructureSourceListing(), StructureEngineFixture.Engine(), new StructureLimits(),
        new StructureJudge(), new SandboxFileReaderFactory());
}
