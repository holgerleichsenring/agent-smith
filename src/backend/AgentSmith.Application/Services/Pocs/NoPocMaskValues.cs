using AgentSmith.Contracts.Pocs;

namespace AgentSmith.Application.Services.Pocs;

/// <summary>2026-11-23-933dg: without a database no PoC variable is stored, so a run masks none.</summary>
public sealed class NoPocMaskValues : IPocMaskValues
{
    public Task<IReadOnlyList<string>> ForProjectAsync(string project, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<string>>([]);
}
