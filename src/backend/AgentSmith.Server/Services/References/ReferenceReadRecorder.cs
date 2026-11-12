using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.References;

/// <summary>
/// 2026-11-04-96a0c: keeps what a finished design turn opened of one upload, and says how much that was. It stores in
/// a scope of its own, like DbConversationNotes, and only the readable files of the set. A store failure is logged and
/// swallowed and the counts come from memory: storing never fails an answer the turn already has.
/// </summary>
public sealed class ReferenceReadRecorder(IServiceScopeFactory scopeFactory, ILogger<ReferenceReadRecorder> logger)
{
    public async Task<UploadProvenance> RecordAsync(ReferenceSetSandbox upload, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(upload);
        var coverage = upload.Coverage ?? ReferenceCoverage.Empty;
        var thisTurn = coverage.ReadableOf(upload.OpenedPaths);
        if (thisTurn.Count > 0) await StoreAsync(upload, thisTurn, ct);
        return coverage.Stamp(upload.RepoName, upload.SetId, thisTurn);
    }

    private async Task StoreAsync(ReferenceSetSandbox upload, IReadOnlyList<string> paths, CancellationToken ct)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<ReferenceReadRepository>()
                .AddAsync(upload.ConversationId, upload.SetId, paths, ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "The reads of upload {Address} (set {SetId}) could not be stored",
                upload.RepoName, upload.SetId);
        }
    }
}
