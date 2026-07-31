using System.ComponentModel;
using AgentSmith.Application.Models;
using AgentSmith.Contracts.WorkSpecs;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// p0390: hosts <c>revise_work_spec</c> — the master amends the spec by writing a
/// NEW REVISION that names its cause, never by a silent edit. Two guards: the
/// done-section is read-only while a ratified expectation exists, and after the
/// first source commit a revision needs a source commit since the previous one.
/// The host owns no I/O; <see cref="WorkSpecReviser"/> commits and pushes.
/// </summary>
public sealed class WorkSpecToolHost(IWorkSpecReviser reviser) : IToolHost
{
    public IEnumerable<AIFunction> GetTools(SkillExecutionPhase? phase, string? investigatorMode)
    {
        _ = phase;
        _ = investigatorMode;
        return [AIFunctionFactory.Create(ReviseWorkSpec, name: "revise_work_spec")];
    }

    [Description(
        "Write a NEW REVISION of this run's work spec. Use it when the code showed a "
        + "requirement, constraint or assumption was wrong or incomplete — the spec is the "
        + "statement of WHAT must be true and it should stay true. Pass the COMPLETE lists "
        + "every time (full-state replacement, not a patch) and say plainly what changed and "
        + "why in 'cause'. The spec carries NO steps and NO file names: those belong to your "
        + "own progress checklist. While this run has a ratified acceptance contract, its "
        + "done-criteria are read-only and your 'done' is ignored.")]
    public Task<string> ReviseWorkSpec(
        [Description("Why this revision exists — one sentence, e.g. 'the named API does not "
            + "exist on this version, the requirement now states the supported equivalent'.")]
        string cause,
        [Description("The complete goal sentence.")] string goal,
        [Description("The complete requirement list — what must be TRUE when the work is done.")]
        IReadOnlyList<string> requirements,
        [Description("The complete constraint list — technical rules, carried verbatim.")]
        IReadOnlyList<string>? constraints = null,
        [Description("The complete assumption list — unresolved points you resolved by choosing.")]
        IReadOnlyList<string>? assumptions = null,
        [Description("The complete done-criteria list. Ignored while a ratified contract exists.")]
        IReadOnlyList<string>? done = null,
        CancellationToken cancellationToken = default) =>
        reviser.ReviseAsync(
            new WorkSpecRevisionRequest(cause, goal, requirements, constraints, assumptions, done),
            cancellationToken);
}
