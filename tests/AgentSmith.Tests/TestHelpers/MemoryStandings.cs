using AgentSmith.Application.Services.Memory;
using AgentSmith.Contracts.Memory;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.TestHelpers;

/// <summary>2026-10-25-9324d: the standing readers over a ledger — the DB-free one unless a test hands its own.</summary>
internal static class MemoryStandings
{
    public static MemoryStandingReader Reader(IMemoryEvidenceStore? store = null) =>
        new(store ?? new NullMemoryEvidenceStore(), new MemoryRepositoryKey(), new MemoryStandingCalculator(),
            NullLogger<MemoryStandingReader>.Instance);

    public static MemoryIndexReader IndexReader(
        AgentSmith.Application.Services.Repositories.RunRepositoriesFactory repositories, IMemoryEvidenceStore? store = null) =>
        new(repositories, Reader(store), new MemoryIndexComposer(), NullLogger<MemoryIndexReader>.Instance);

    public static MemoryIndexLabelWriter LabelWriter(IMemoryEvidenceStore? store = null) =>
        new(Reader(store), new MemoryIndexLabels());
}
