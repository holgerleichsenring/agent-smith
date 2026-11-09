using AgentSmith.Contracts.Prompts;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Persistence.Services.Prompts;

/// <summary>
/// 2026-11-09-0620a: stores what a model call was sent. Blocks are written once; a call that extends a recent record of
/// its run stores only the blocks it appended. Writes in one process are serialised, so parallel sub-agents sending the
/// same tool list do not race to insert it; a race with another replica is retried once. A failure is logged and the
/// call goes out unrecorded.
/// </summary>
public sealed class PromptRecorder(
    IServiceScopeFactory scopeFactory,
    PromptRecentRecords recent,
    ILogger<PromptRecorder> logger) : IPromptRecorder
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task<string?> RecordAsync(
        string runId, IReadOnlyList<PromptBlock> blocks, int toolCount, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(runId) || blocks.Count == 0) return null;
        var hashes = blocks.Select(PromptHashes.Of).ToList();
        var id = PromptHashes.Record(runId, hashes);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    await StoreAsync(runId, id, blocks, hashes, toolCount, cancellationToken);
                    recent.Remember(runId, id, hashes);
                    return id;
                }
                catch (DbUpdateException) when (attempt == 1) { }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Could not store the prompt of a model call of run {RunId}.", runId);
            return null;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task StoreAsync(
        string runId, string id, IReadOnlyList<PromptBlock> blocks, List<string> hashes, int toolCount,
        CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        if (await uow.Set<PromptRecord>().AnyAsync(r => r.Id == id, ct)) return;
        var parent = recent.LongestPrefixOf(runId, hashes);
        var from = parent?.Length ?? 0;
        uow.Add(new PromptRecord
        {
            Id = id, RunId = runId, ParentId = parent?.Id, ParentLength = from, ToolCount = toolCount,
        });
        var own = Enumerable.Range(from, blocks.Count - from).ToList();
        var wanted = own.Select(i => hashes[i]).Distinct().ToList();
        var stored = await uow.Set<PromptBlockRow>().Where(b => wanted.Contains(b.Hash)).Select(b => b.Hash).ToListAsync(ct);
        foreach (var i in own.Where(i => !stored.Contains(hashes[i])).DistinctBy(i => hashes[i]))
            uow.Add(Row(hashes[i], blocks[i]));
        foreach (var i in own)
            uow.Add(new PromptRecordBlock { RecordId = id, Position = i, BlockHash = hashes[i] });
        await uow.SaveChangesAsync(ct);
    }

    private static PromptBlockRow Row(string hash, PromptBlock block) => new()
    {
        Hash = hash, Kind = block.Kind.ToString(), Role = block.Role, Chars = block.Content.Length,
        Content = block.Content,
    };
}
