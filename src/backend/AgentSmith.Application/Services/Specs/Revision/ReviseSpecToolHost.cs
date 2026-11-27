using System.ComponentModel;
using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Commands;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Specs.Revision;

/// <summary>
/// 2026-11-17-ec48a: revise_spec — the coding master revises the spec it is building instead of pausing the run. Built
/// only for a master executing a spec (<see cref="MasterRecordToolHosts"/>). 2026-11-19-3e10: the text is checked and
/// written; no reviewer judges it. A second opinion is a sub-agent the master briefs itself. 2026-11-27-6625: a repository
/// the revision moves the spec to joins through the master's own ensure_repo_sandbox (<see cref="JoiningThrough"/>).
/// 2026-11-27-3532d: it retires the spec for a replacement (<see cref="SpecRetirement"/>).
/// </summary>
public sealed class ReviseSpecToolHost(
    MasterSpecRevision revision, PipelineContext pipeline, Func<string, CancellationToken, Task<string>>? ensure = null)
    : IToolHost
{
    public const string Name = "revise_spec";

    private const string Described =
        "Revises the spec you are building, on the ticket branch: write it when no spec was written yet, or when the "
        + "operator's message or a premise the code proved false changes what it must make true. Pass the WHOLE spec "
        + "yaml — same spec id and requires — and the reason in one sentence. The text is checked against the spec "
        + "schema and written as the next revision; its done criteria are the ones you report on. While you work the "
        + "spec, its repos and applies_to can be corrected too: a repository it now names joins the workspace as with "
        + "ensure_repo_sandbox, the spec is written there, and your file writes follow it. A spec you find cannot be "
        + "implemented as written can be retired with retire: true: spec_yaml is then its replacement, which gets the "
        + "series' next id, takes its place and is what later specs require, while the retired spec stays on the branch "
        + "as superseded. A spec that already ran is never revised or retired; a correction to one is a new spec. Want "
        + "a second opinion first? Brief a reviewer with spawn_agents.";

    /// <summary>2026-11-27-6625: this tool, bringing a repository the spec moves to in through <paramref name="join"/>.</summary>
    public ReviseSpecToolHost JoiningThrough(Func<string, CancellationToken, Task<string>> join) => new(revision, pipeline, join);

    public IEnumerable<AIFunction> GetTools(SkillExecutionPhase? stage, string? investigatorMode)
    {
        _ = stage;
        _ = investigatorMode;
        return [AIFunctionFactory.Create(ReviseSpec, new AIFunctionFactoryOptions { Name = Name, Description = Described })];
    }

    public Task<string> ReviseSpec(
        [Description("The whole revised spec yaml.")] string spec_yaml,
        [Description("Why the spec changes, in one sentence: the operator's message or the premise it answers.")] string reason,
        [Description("True to retire the spec you build and write spec_yaml as its replacement.")] bool retire = false,
        CancellationToken ct = default) =>
        revision.ReviseAsync(pipeline, spec_yaml, reason, ct, ensure, retire);
}
