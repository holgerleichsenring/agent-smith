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

    /// <summary>2026-11-12-cd4cg: the spec's schema, carried by the tool instead of a fetched format.</summary>
    internal const string SpecSchema =
        "The spec as YAML, English whatever the conversation's language. goal (required): one terse sentence. "
        + "repos: the one repository it changes, as the prompt lists it (a parent names none). scope: {in, out} and no "
        + "other key. steps: [{id, action}] with one imperative line each. decisions: lines; a choice you make where "
        + "the code leaves it open starts with 'proposed:'. tests: Method_Scenario_Expected names. done: what a "
        + "reviewer answers yes or no against the code, one line each, or {given?, when, then}. facts: [{claim, "
        + "evidence}] for what you read, evidence a repository-relative path with lines. assumptions: what it rests on "
        + "that you did not read. requires: ids of specs in this draft (a child names siblings, never the parent), "
        + "or a sentence for an external precondition.";

    public IEnumerable<AIFunction> GetTools(SkillExecutionPhase? stage, string? investigatorMode)
    {
        _ = stage;
        _ = investigatorMode;
        return [AIFunctionFactory.Create(WriteSpec, name: Name), AIFunctionFactory.Create(WriteContract, name: ContractName)];
    }

    [Description(
        "Write one spec into this turn's draft. Leave 'spec' out to write a new one: the answer names the id it got. "
        + "State 'spec: <id>' to rewrite that spec. One spec is a spec proposal; several are an epic: write the parent "
        + "with parent=true first, then each child. The answer is the exact check of this spec; nothing else checks it.")]
    private string WriteSpec(
        [Description(SpecSchema)] string yaml,
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

    [Description("Write the epic's series contract over the draft's children: what each child owns that the others use.")]
    private string WriteContract(
        [Description("{names: [{name, kind, number?, owner, writers?}], data: [{datum, lives_in, owner, writers?}], "
            + "areas: [{spec, paths}], budgets: [{path, spec, max_lines}]}; owner, writers and spec name a child by its "
            + "id; kind is one of event, system-event, table, context-key, endpoint, config, command, tool, type; no '*' "
            + "in a path.")] JsonElement contract)
    {
        var refusal = store.Apply(draft => contracts.Write(draft, contract));
        return DraftWriteAnswer.Of(refusal, "Stored the series contract.", store);
    }
}
