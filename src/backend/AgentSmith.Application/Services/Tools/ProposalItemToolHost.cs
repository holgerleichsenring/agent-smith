using System.ComponentModel;
using AgentSmith.Application.Models;
using AgentSmith.Application.Services.SpecDialog.Writes;
using AgentSmith.Contracts.Models;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Tools;

/// <summary>2026-11-12-cd4ca: remove_item — takes one item out of the turn's draft by its key, id, or bug/comment/contract.</summary>
public sealed class ProposalItemToolHost(ProposalStore store) : IToolHost
{
    public const string Name = "remove_item";

    public IEnumerable<AIFunction> GetTools(SkillExecutionPhase? stage, string? investigatorMode)
    {
        _ = stage;
        _ = investigatorMode;
        return [AIFunctionFactory.Create(RemoveItem, name: Name)];
    }

    [Description("Remove one item from this turn's draft: a ticket's key, a spec's id, or bug, comment or contract.")]
    private string RemoveItem([Description("The item's key or id.")] string item)
    {
        var refusal = store.Apply(draft => ProposalItemRemoval.Remove(draft, item));
        return DraftWriteAnswer.Of(refusal, $"Removed '{(item ?? string.Empty).Trim()}'.", store);
    }
}
