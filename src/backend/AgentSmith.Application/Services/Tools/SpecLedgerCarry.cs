using System.Security.Cryptography;
using System.Text;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Progress;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// 2026-11-24-0fbfa: the ledger a master step opens on. A spec's later pass — the structure correction, a repair, the
/// pass after an answer — is a new master step of the same spec, and seeding it afresh set every step the spec had
/// finished back to pending: the run page read as a new start and the open loop re-drove the master to redo them. A
/// ledger the pipeline holds for the same spec, seeded from the same steps, is carried as it stands — the master may
/// have restructured it (p0359), so it is not merged with the seed. Another spec, a spec whose steps were revised
/// since, or no spec is seeded from its steps.
/// </summary>
public static class SpecLedgerCarry
{
    /// <summary>The seed for <paramref name="draft"/>: its own ledger as it stands, else its steps, all pending.</summary>
    public static IReadOnlyList<ProgressLedgerEntry> Seed(PipelineContext pipeline, SpecDraft? draft)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        var fresh = ProgressLedgerSeeder.Seed(draft);
        if (draft is null) return fresh;
        var owned = pipeline.TryGet<string>(ContextKeys.ProgressLedgerSpecId, out var held) && held == OwnerOf(draft);
        return owned && pipeline.TryGet<ProgressLedger>(ContextKeys.ProgressLedger, out var ledger) && ledger is { IsEmpty: false }
            ? ledger.Entries
            : fresh;
    }

    /// <summary>Records which spec, and which of its step lists, the pipeline's ledger belongs to; none for no spec.</summary>
    public static void Own(PipelineContext pipeline, SpecDraft? draft)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        if (draft is null) pipeline.Remove(ContextKeys.ProgressLedgerSpecId);
        else pipeline.Set(ContextKeys.ProgressLedgerSpecId, OwnerOf(draft));
    }

    // The spec id and a digest of its steps: a revise_spec keeps the id and changes the steps.
    private static string OwnerOf(SpecDraft draft)
    {
        var steps = string.Join('\n', draft.Steps.Select(s => $"{s.Id}\t{s.Action}\t{s.Target}"));
        return $"{draft.SpecId}:{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(steps)))[..16]}";
    }
}
