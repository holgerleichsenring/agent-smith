using AgentSmith.Contracts.Prompts;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Infrastructure.Persistence.Services.Prompts;

/// <summary>
/// 2026-11-09-0620l: writes one queued prompt. A block another replica inserts first is retried once; after the write,
/// any block the record names but the store lacks — a retention sweep can remove an orphan between the existence check
/// and the record rows — is written again, so a stored record never points at nothing.
/// </summary>
internal sealed class PromptRecordWriter(IServiceScopeFactory scopeFactory)
{
    public async Task StoreAsync(PromptWrite write, CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await StoreOnceAsync(write, ct);
                await RestoreMissingAsync(write, ct);
                return;
            }
            catch (DbUpdateException) when (attempt == 1) { }
        }
    }

    private async Task StoreOnceAsync(PromptWrite write, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        if (await uow.Set<PromptRecord>().AnyAsync(r => r.Id == write.Id, ct)) return;
        uow.Add(new PromptRecord
        {
            Id = write.Id, RunId = write.RunId, ParentId = write.ParentId, ParentLength = write.ParentLength,
            ToolCount = write.ToolCount,
        });
        var own = Enumerable.Range(write.ParentLength, write.Blocks.Count - write.ParentLength).ToList();
        AddMissingBlocks(uow, write, own, await StoredAsync(uow, write, own, ct));
        foreach (var i in own)
            uow.Add(new PromptRecordBlock { RecordId = write.Id, Position = i, BlockHash = write.Hashes[i] });
        await uow.SaveChangesAsync(ct);
    }

    private async Task RestoreMissingAsync(PromptWrite write, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var own = Enumerable.Range(write.ParentLength, write.Blocks.Count - write.ParentLength).ToList();
        var stored = await StoredAsync(uow, write, own, ct);
        if (own.All(i => stored.Contains(write.Hashes[i]))) return;
        AddMissingBlocks(uow, write, own, stored);
        await uow.SaveChangesAsync(ct);
    }

    private static async Task<HashSet<string>> StoredAsync(IUnitOfWork uow, PromptWrite write, List<int> own, CancellationToken ct)
    {
        var wanted = own.Select(i => write.Hashes[i]).Distinct().ToList();
        return [.. await uow.Set<PromptBlockRow>().Where(b => wanted.Contains(b.Hash)).Select(b => b.Hash).ToListAsync(ct)];
    }

    private static void AddMissingBlocks(IUnitOfWork uow, PromptWrite write, List<int> own, HashSet<string> stored)
    {
        foreach (var i in own.Where(i => !stored.Contains(write.Hashes[i])).DistinctBy(i => write.Hashes[i]))
            uow.Add(Row(write.Hashes[i], write.Blocks[i]));
    }

    private static PromptBlockRow Row(string hash, PromptBlock block) => new()
    {
        Hash = hash, Kind = block.Kind.ToString(), Role = block.Role, Chars = block.Content.Length,
        Content = block.Content,
    };
}
