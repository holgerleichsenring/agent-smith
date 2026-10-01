using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>2026-10-01-283dc: the default with no durable store — there are no uploaded sets to read.</summary>
public sealed class NoReferenceSetReader : IReferenceSetReader
{
    public Task<IReadOnlyList<ReferenceSetFile>> FilesAsync(
        string sessionId, string setId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ReferenceSetFile>>([]);

    public Task<IReadOnlyList<string>> SetIdsAsync(string sessionId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<string>>([]);
}
