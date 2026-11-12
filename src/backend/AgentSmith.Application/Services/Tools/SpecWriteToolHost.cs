using System.ComponentModel;
using System.Text.Json;
using AgentSmith.Application.Models;
using AgentSmith.Application.Services.SpecDialog.Writes;
using AgentSmith.Contracts.Models;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// 2026-11-12-cd4ca: write_spec — one spec into the turn's draft, its id minted by the store or the spec it rewrites;
/// and write_contract — an epic's series contract over the draft's children.
/// </summary>
public sealed class SpecWriteToolHost(
    ProposalStore store, SpecDraftWrite write, ContractDraftWrite contracts, IReadOnlyList<string> scope) : IToolHost
{
    public const string Name = "write_spec";
    public const string ContractName = "write_contract";

    public IEnumerable<AIFunction> GetTools(SkillExecutionPhase? stage, string? investigatorMode)
    {
        _ = stage;
        _ = investigatorMode;
        return [AIFunctionFactory.Create(WriteSpec, name: Name), AIFunctionFactory.Create(WriteContract, name: ContractName)];
    }

    [Description(
        "Write one spec into this turn's draft. Leave 'spec' out to write a new one: the answer names the id it got. "
        + "State 'spec: <id>' to rewrite that spec. One spec is a spec proposal; several are an epic: write the parent "
        + "with parent=true first, then each child.")]
    private string WriteSpec(
        [Description("The spec as YAML: goal, repos, scope{in,out}, steps[{id,action}], decisions, tests, done, facts[{claim,evidence}], assumptions, requires (ids of specs in this draft).")] string yaml,
        [Description("True for an epic's parent, which aggregates its children and changes no repository itself.")] bool parent = false)
    {
        string? id = null;
        var refusal = store.Apply(draft =>
        {
            var result = write.Write(draft, yaml ?? string.Empty, parent, scope);
            id = result.Handle;
            return result;
        });
        return DraftWriteAnswer.Of(refusal, $"Stored spec '{id}'.", store);
    }

    [Description("Write the epic's series contract: names, data, areas and budgets, each naming a child by its id.")]
    private string WriteContract([Description("The contract object.")] JsonElement contract)
    {
        var refusal = store.Apply(draft => contracts.Write(draft, contract));
        return DraftWriteAnswer.Of(refusal, "Stored the series contract.", store);
    }
}
