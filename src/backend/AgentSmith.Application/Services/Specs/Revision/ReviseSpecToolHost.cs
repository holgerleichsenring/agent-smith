using System.ComponentModel;
using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Commands;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Specs.Revision;

/// <summary>
/// 2026-11-17-ec48a: revise_spec — the coding master revises the spec it is building instead of pausing the run. Built
/// only for a master executing a spec (<see cref="MasterRecordToolHosts"/>). 2026-11-19-3e10: the text is checked and
/// written; no reviewer judges it. A second opinion is a sub-agent the master briefs itself.
/// </summary>
public sealed class ReviseSpecToolHost(MasterSpecRevision revision, PipelineContext pipeline) : IToolHost
{
    public const string Name = "revise_spec";

    private const string Described =
        "Revises the spec you are building, on the ticket branch: write it when no spec was written yet, or when the "
        + "operator's message or a premise the code proved false changes what it must make true. Pass the WHOLE spec "
        + "yaml — same spec id, requires and repos — and the reason in one sentence. The text is checked against the "
        + "spec schema and written as the next revision; its done criteria are the ones you report on. A spec that "
        + "already ran is never revised; a correction to one is a new spec. Want a second opinion first? Brief a "
        + "reviewer with spawn_agents.";

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
        revision.ReviseAsync(pipeline, spec_yaml, reason, ct);
}
