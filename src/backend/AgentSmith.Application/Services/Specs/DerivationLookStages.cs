using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-09-20-9c74: the verify stages the repositories of a run DECLARED, in the two forms a
/// look needs them.
/// <para>
/// The LABELS are read while the prompt is written — synchronously, off a declaration the
/// factory already read out of the pipeline — because the prompt section renders inside a raw
/// string and the gate's resolution is asynchronous and needs a sandbox. Only one of the two
/// filters can be applied that early: the cannot-fail check is a pure static, and it is the
/// one that removes a WHOLE repository, which is the case where every label listed would be a
/// lie. The presence check needs a sandbox and is applied on use.
/// </para>
/// <para>
/// What may RUN is the gate's own answer, taken and not copied: the same resolver, called with
/// a NULL project map so the analyzer's inferred pair collapses to nothing — a null command
/// cannot fail. Reading the declaration raw instead would offer a stage the gate refuses, run
/// it, and mint the same false green the gate was taught to refuse.
/// </para>
/// </summary>
public sealed class DerivationLookStages(
    VerifyStageResolver resolver,
    IReadOnlyDictionary<string, IReadOnlyList<ContextVerifyStages>> declared)
{
    private readonly Dictionary<string, IReadOnlyList<VerifyStage>> _resolved =
        new(StringComparer.Ordinal);

    /// <summary>
    /// The labels this repository declared, as declared — empty when it declared nothing, and
    /// empty when any one of its declarations cannot fail, because that takes the whole
    /// repository out of the gate's resolution and out of this one.
    /// </summary>
    public IReadOnlyList<string> Labels(string repository)
    {
        var stages = For(repository).SelectMany(context => context.Stages).ToList();
        return stages.TrueForAll(stage => VerificationCommand.CanFail(stage.Command))
            ? [.. stages.Select(stage => stage.Label)]
            : [];
    }

    /// <summary>
    /// What the gate would run in this repository as the tree stands now, resolved on first
    /// use and remembered: a repository is asked about once per look-set in the ordinary case,
    /// and a refused label must not buy a second resolution.
    /// </summary>
    public async Task<IReadOnlyList<VerifyStage>> RunnableAsync(
        string repository, ISandbox sandbox, CancellationToken ct)
    {
        if (_resolved.TryGetValue(repository, out var remembered)) return remembered;
        var stages = await resolver.ResolveAsync(
            repository, map: null, sandbox, For(repository), new VerifyResolutionNotes(), ct);
        _resolved[repository] = stages;
        return stages;
    }

    private IReadOnlyList<ContextVerifyStages> For(string repository) =>
        declared.TryGetValue(repository, out var contexts) ? contexts : [];
}
