using System.Text.Json;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.SpecDialog.Writes;

/// <summary>
/// 2026-11-12-cd4ca: an epic's series contract written over the draft's children, checked as the series checks it.
/// A draft with no parent has no series to hold a contract. 2026-11-15-67f1: <c>source</c> is the file text written,
/// kept as the contract's Source.
/// </summary>
public sealed class ContractDraftWrite(SeriesContractReader reader)
{
    public DraftWrite Write(ProposalDraft draft, JsonElement contract, string? source = null)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if (!draft.Specs.Any(s => s.Parent))
            return DraftWrite.Refused($"a series contract belongs to an epic — write its parent as {DraftPath.Prefix}{DraftPath.Epic}<id>.yaml first");
        var read = reader.FromDraft(contract, [.. draft.Specs.Where(s => !s.Parent).Select(s => s.Draft)]);
        if (read.Problem is { } problem) return DraftWrite.Refused(problem);
        return read.Contract is null
            ? DraftWrite.Refused($"the series contract is empty — delete {DraftPath.Prefix}{DraftPath.Contract} to drop it")
            : DraftWrite.To(draft with { Contract = read.Contract with { Source = source } });
    }
}
