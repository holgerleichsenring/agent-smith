using AgentSmith.Application.Services.Memory;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.TestHelpers;

/// <summary>2026-10-25-9324c: the run-end outcome over the DB-free store, for handlers whose test is not about it.</summary>
internal static class MemoryOutcomes
{
    public static MemoryOutcomeRecorder None() =>
        new(new NullMemoryEvidenceStore(), NullLogger<MemoryOutcomeRecorder>.Instance);
}
