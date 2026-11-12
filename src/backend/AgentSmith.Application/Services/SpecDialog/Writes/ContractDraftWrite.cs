using System.Text.Json;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.SpecDialog.Writes;

/// <summary>
/// 2026-11-12-cd4ca: an epic's series contract written over the draft's children, checked as the series checks it.
/// A draft with no parent has no series to hold a contract.
/// </summary>
public sealed class ContractDraftWrite(SeriesContractReader reader)
{
    public DraftWrite Write(ProposalDraft draft, JsonElement contract)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if (!draft.Specs.Any(s => s.Parent))
            return DraftWrite.Refused("a series contract belongs to an epic — write its parent with parent: true first");
        var read = reader.FromDraft(contract, [.. draft.Specs.Where(s => !s.Parent).Select(s => s.Draft)]);
        if (read.Problem is { } problem) return DraftWrite.Refused(problem);
        return read.Contract is null
            ? DraftWrite.Refused("the series contract is empty — remove_item contract to drop it")
            : DraftWrite.To(draft with { Contract = read.Contract });
    }
}
