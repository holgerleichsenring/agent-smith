using AgentSmith.Contracts.Prompts;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Services.Prompts;

/// <summary>
/// 2026-11-09-0620a: rebuilds one call's prompt in send order. The call's record holds the blocks it appended; the
/// rest it inherits from its parent chain, each record keeping the first <c>ParentLength</c> blocks of the one before.
/// A chain cut by retention yields what survives, marked incomplete.
/// </summary>
public sealed class PromptRecordReader(IUnitOfWork unitOfWork)
{
    public async Task<PromptRecordView?> ReadAsync(string runId, string promptId, CancellationToken ct)
    {
        var records = await unitOfWork.Set<PromptRecord>().AsNoTracking()
            .Where(r => r.RunId == runId).ToDictionaryAsync(r => r.Id, ct);
        if (!records.TryGetValue(promptId, out var call)) return null;

        var chain = PromptChain.Of(call, records, out var complete);
        var ids = chain.Select(r => r.Id).ToList();
        var held = await unitOfWork.Set<PromptRecordBlock>().AsNoTracking()
            .Where(b => ids.Contains(b.RecordId)).ToListAsync(ct);

        var positions = PromptChain.Positions(chain, held);

        var hashes = positions.Values.Distinct().ToList();
        var rows = await unitOfWork.Set<PromptBlockRow>().AsNoTracking()
            .Where(b => hashes.Contains(b.Hash)).ToDictionaryAsync(b => b.Hash, ct);
        var blocks = positions
            .Where(p => rows.ContainsKey(p.Value))
            .Select(p => View(p.Key, rows[p.Value]))
            .ToList();
        return new PromptRecordView(call.Id, call.ToolCount, complete && blocks.Count == positions.Count, blocks);
    }

    private static PromptBlockView View(int position, PromptBlockRow row) =>
        new(position, row.Kind, row.Role, row.Chars, row.Content);
}
