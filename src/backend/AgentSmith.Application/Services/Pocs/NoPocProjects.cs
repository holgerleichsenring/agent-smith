using AgentSmith.Contracts.Pocs;

namespace AgentSmith.Application.Services.Pocs;

/// <summary>2026-11-23-933db: a composition without a database holds no Poc rows, so no project is a PoC project.</summary>
public sealed class NoPocProjects : IPocProjects
{
    public Task<bool> IsAsync(string project, CancellationToken ct) => Task.FromResult(false);
}
