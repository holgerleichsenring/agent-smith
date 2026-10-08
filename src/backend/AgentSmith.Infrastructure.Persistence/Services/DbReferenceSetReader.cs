using AgentSmith.Contracts.Sandbox;
using AgentSmith.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Infrastructure.Persistence.Services;

/// <summary>
/// 2026-10-01-283dc: <see cref="IReferenceSetReader"/> for the singleton sandbox path. It opens a
/// scope per read and delegates to the scoped repository, like the other Db stores.
/// </summary>
public sealed class DbReferenceSetReader(IServiceScopeFactory scopeFactory) : IReferenceSetReader
{
    public async Task<IReadOnlyList<ReferenceSetFile>> FilesAsync(
        string sessionId, string setId, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var files = await scope.ServiceProvider.GetRequiredService<ReferenceSetRepository>()
            .FilesAsync(sessionId, setId, cancellationToken);
        return [.. files.Select(f => new ReferenceSetFile(f.Path, f.Content))];
    }

    /// <summary>2026-10-08-e8b9j: one row, not the set.</summary>
    public async Task<ReferenceSetFile?> FileAsync(
        string sessionId, string setId, string path, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var file = await scope.ServiceProvider.GetRequiredService<ReferenceSetRepository>()
            .FileAsync(sessionId, setId, path, cancellationToken);
        return file is null ? null : new ReferenceSetFile(path, file);
    }

    /// <summary>2026-10-08-e8b9k.</summary>
    public async Task<IReadOnlyList<string>> ImageSetIdsAsync(string sessionId, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        return [.. (await scope.ServiceProvider.GetRequiredService<ReferenceFileRepository>()
            .ListImagesAsync(sessionId, cancellationToken)).Where(i => i.SetId is not null).Select(i => i.SetId!)];
    }

    /// <summary>2026-10-08-e8b9k.</summary>
    public async Task<ReferenceImageFile?> ImageAsync(string sessionId, string setId, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ReferenceFileRepository>()
            .ImageBySetAsync(sessionId, setId, cancellationToken);
    }

    public async Task<IReadOnlyList<string>> SetIdsAsync(string sessionId, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var sets = await scope.ServiceProvider.GetRequiredService<ReferenceSetRepository>()
            .ListAsync(sessionId, cancellationToken);
        return [.. sets.Select(s => s.SetId)];
    }
}
