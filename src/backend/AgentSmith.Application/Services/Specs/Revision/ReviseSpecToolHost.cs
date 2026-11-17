using System.ComponentModel;
using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Specs.Revision;

/// <summary>
/// 2026-11-17-ec48a: revise_spec — the coding master revises the spec it is building, with one review of the changed
/// text, instead of pausing the run. Built only for a master executing a spec (<see cref="MasterRecordToolHosts"/>).
/// </summary>
public sealed class ReviseSpecToolHost(MasterSpecRevision revision, PipelineContext pipeline, AgentConfig agent) : IToolHost
{
    public const string Name = "revise_spec";

    private const string Described =
        "Revises the spec you are building, on the ticket branch. Use it when the operator's message, or a premise the "
        + "code proved false, changes what this spec must make true. Pass the WHOLE revised spec yaml — same spec id, "
        + "requires, repos and applies_to — and the reason in one sentence. The text is checked against the spec "
        + "schema, then one fresh review decides: BUILD or BUILD WITH CUTS writes it as the next revision of the series "
        + "and its done criteria become the ones the verification judges; REFUSE writes nothing and returns the "
        + "reviewers' findings. A spec that already ran is never revised; a correction to one is a new spec.";

    public IEnumerable<AIFunction> GetTools(SkillExecutionPhase? stage, string? investigatorMode)
    {
        _ = stage;
        _ = investigatorMode;
        return [AIFunctionFactory.Create(ReviseSpec, new AIFunctionFactoryOptions { Name = Name, Description = Described })];
    }

    public Task<string> ReviseSpec(
        [Description("The whole revised spec yaml.")] string spec_yaml,
        [Description("Why the spec changes, in one sentence: the operator's message or the premise it answers.")] string reason,
        CancellationToken ct = default) =>
        revision.ReviseAsync(pipeline, agent, spec_yaml, reason, ct);
}
