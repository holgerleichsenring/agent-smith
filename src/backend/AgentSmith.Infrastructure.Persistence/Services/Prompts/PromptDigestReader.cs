using AgentSmith.Contracts.Prompts;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Services.Prompts;

/// <summary>
/// 2026-11-18-e06bd: what a list of a run's calls were fed and decided, in one read of the run's records. A call's fed
/// line is its last message; its decided line is the answer the next call of its tool loop carries — the record whose
/// parent it is and which inherits all of its blocks holds that answer at the first position it appends. The last call
/// of a loop has no such successor, so its decided line is null. An unknown id is left out.
/// </summary>
public sealed class PromptDigestReader(IUnitOfWork unitOfWork)
{
    private const int Context = 6; // not a limit: the tool call a result answers sits in the messages just before it

    public async Task<IReadOnlyList<PromptDigestView>> ReadAsync(
        string runId, IEnumerable<string> promptIds, CancellationToken ct)
    {
        var records = await unitOfWork.Set<PromptRecord>().AsNoTracking()
            .Where(r => r.RunId == runId).ToDictionaryAsync(r => r.Id, ct);
        var calls = promptIds.Distinct().Where(records.ContainsKey).Select(id => records[id]).ToList();
        if (calls.Count == 0) return [];

        var chains = calls.ToDictionary(c => c.Id, c => PromptChain.Of(c, records, out _));
        var held = await HeldAsync(chains.Values.SelectMany(c => c).Select(r => r.Id), ct);
        var positions = chains.ToDictionary(c => c.Key, c => PromptChain.Positions(c.Value, held));
        var answers = calls.ToDictionary(c => c.Id, c => AnswerOf(c, Length(positions[c.Id]), records.Values));
        var answerBlocks = await HeldAsync(answers.Values.OfType<PromptRecord>().Select(r => r.Id), ct);

        var wanted = positions.Values.SelectMany(p => p.Reverse().Take(Context).Select(kv => kv.Value))
            .Concat(answerBlocks.Select(b => b.BlockHash)).Distinct().ToList();
        var rows = await unitOfWork.Set<PromptBlockRow>().AsNoTracking()
            .Where(b => wanted.Contains(b.Hash)).ToDictionaryAsync(b => b.Hash, ct);
        return calls.Select(c => Digest(c.Id, positions[c.Id], answers[c.Id], answerBlocks, rows)).ToList();
    }

    private async Task<List<PromptRecordBlock>> HeldAsync(IEnumerable<string> recordIds, CancellationToken ct)
    {
        var ids = recordIds.Distinct().ToList();
        return await unitOfWork.Set<PromptRecordBlock>().AsNoTracking().Where(b => ids.Contains(b.RecordId)).ToListAsync(ct);
    }

    private static int Length(SortedDictionary<int, string> positions) => positions.Count == 0 ? 0 : positions.Keys.Max() + 1;

    private static PromptRecord? AnswerOf(PromptRecord call, int length, IEnumerable<PromptRecord> records) =>
        length == 0 ? null : records.FirstOrDefault(r => r.ParentId == call.Id && r.ParentLength == length);

    private static PromptDigestView Digest(
        string id, SortedDictionary<int, string> positions, PromptRecord? answer,
        IReadOnlyCollection<PromptRecordBlock> answerBlocks, IReadOnlyDictionary<string, PromptBlockRow> rows)
    {
        var messages = positions.Reverse().Take(Context).Reverse()
            .Select(p => rows.GetValueOrDefault(p.Value)).OfType<PromptBlockRow>()
            .Where(r => r.Kind == nameof(PromptBlockKind.Message)).ToList();
        var fed = messages.Count == 0 ? null
            : PromptDigestLines.Fed(messages[^1].Role, messages[^1].Content, messages.SkipLast(1).Select(m => m.Content));
        var block = answer is null ? null
            : answerBlocks.FirstOrDefault(b => b.RecordId == answer.Id && b.Position == answer.ParentLength);
        var decided = block is not null && rows.TryGetValue(block.BlockHash, out var row) ? PromptDigestLines.Decided(row.Content) : null;
        return new PromptDigestView(id, fed, decided);
    }
}
