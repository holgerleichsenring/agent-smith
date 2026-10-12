using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Application.Services.Structure;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Structure;

/// <summary>
/// 2026-10-06-8565c: a baseline service over real collaborators. A round whose principles carry
/// no Limits takes none, which is what every pre-existing round test composes.
/// 2026-10-13-2d0c: and the instance services the structure readers share.
/// </summary>
internal static class StructureBaselineStubs
{
    public static StructureBaselineFile File { get; } = new(new StructureBaselineHeader());

    public static StructureGitReads GitReads() => new(new StructureGitRunner(), new StructureZeroListing());

    public static StructureSourceListing Listing() => new(new StructureGitRunner(), new StructureZeroListing());

    public static StructureBaseline Real(IStructureEngine? engine = null) => new(
        Listing(), engine ?? StructureEngineFixture.Engine(), new StructureLimits(),
        new StructureJudge(), File, new SandboxFileReaderFactory(), NullLogger<StructureBaseline>.Instance);
}
