using System.ComponentModel;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Sandbox;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// run_gate_stage: the coding master runs any stage a context of the run DECLARED, in THAT
/// context's own sandbox, over its levelled work, before it gives its verdict. The master's
/// run_command reaches only a repository's first sandbox, so without this a stage of another
/// toolchain image could first run at verification. Nothing the model writes becomes a
/// command: the label picks a declared line. when_changed does not filter what is asked for.
/// </summary>
public sealed class GateStageToolHost(
    SandboxTreeSync treeSync, SandboxTargets targets, ContextVerifyStagesResolver declared,
    VerifyStageResolver resolver, VerifyCommandRunner runner)
{
    public const string Name = "run_gate_stage";

    private const string Description =
        "Runs one verify stage a context of this run DECLARED — by repository and label, exactly the "
        + "declared command, in that context's own sandbox — after levelling every sandbox of the run "
        + "to the union of the work, and returns the exit code with the end of the output. Any "
        + "non-zero exit is red. Name the context when two contexts declare the label. A stage of a "
        + "repository's first sandbox can also be run with run_command, which needs no levelling; a "
        + "checkpoint the secret scan declines makes every later levelling red.";

    /// <summary>Every declared stage of the run as (repository, context, label), in sandbox order.
    /// Empty exactly when the tool is not offered, so the prompt block and the tool agree.</summary>
    public static IReadOnlyList<(string Repository, string Context, string Label)> Declared(PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        if (!pipeline.TryGet<IReadOnlyDictionary<string, string>>(ContextKeys.SandboxRepos, out var owners)
            || owners is null)
            return [];
        return [.. owners
            .SelectMany(owner => SandboxContextList.In(pipeline, owner.Key)
                .SelectMany(context => (context.Verify ?? [])
                    .Select(stage => (owner.Value, context.ContextName, stage.Label))))
            .Distinct()];
    }

    public IEnumerable<AIFunction> Tools(PipelineContext pipeline) =>
        Declared(pipeline).Count == 0
            ? []
            : [AIFunctionFactory.Create(
                ([Description("The repository the stage is declared in.")] string repository,
                 [Description("The LABEL of a declared stage, e.g. 'build' or 'test'.")] string label,
                 [Description("The context that declares it; needed only when two contexts declare the label.")]
                 string? context = null,
                 CancellationToken ct = default) => RunAsync(pipeline, repository, label, context, ct),
                Name, Description)];

    public async Task<string> RunAsync(
        PipelineContext pipeline, string repository, string label, string? context, CancellationToken ct)
    {
        var sync = await treeSync.SyncAsync(pipeline, ct);
        if (!sync.IsLevel) return sync.Message!;
        if (!targets.TryResolve(pipeline, out var sandboxes, out _)
            || !pipeline.TryGet<IReadOnlyDictionary<string, string>>(ContextKeys.SandboxRepos, out var owners)
            || owners is null)
            return "Error: this run has no sandbox, so nothing was run.";

        var (pairs, matches) = Match(pipeline, owners, repository, label, context);
        if (matches.Count == 0)
            return $"Error: {repository} declares no stage '{label}'{(context is null ? "" : $" in context '{context}'")}. "
                   + Listing(pairs) + " Nothing was run.";
        if (matches.Count > 1)
            return $"Error: '{label}' is declared by several contexts of {repository}; name one as context. "
                   + Listing(pairs) + " Nothing was run.";
        return await RunOneAsync(pipeline, repository, matches[0].Key, sandboxes[matches[0].Key], matches[0].Context, ct);
    }

    private (List<string> Pairs, List<(string Key, ContextVerifyStages Context)> Matches) Match(
        PipelineContext pipeline, IReadOnlyDictionary<string, string> owners, string repository, string label,
        string? context)
    {
        var pairs = new List<string>();
        var matches = new List<(string Key, ContextVerifyStages Context)>();
        foreach (var key in owners.Where(o => Same(o.Value, repository)).Select(o => o.Key))
        foreach (var contextStages in declared.For(pipeline, key))
        foreach (var stage in contextStages.Stages)
        {
            pairs.Add($"{contextStages.ContextName}/{stage.Label}");
            if (Same(stage.Label, label) && (context is null || Same(contextStages.ContextName, context)))
                matches.Add((key, contextStages with { Stages = [stage] }));
        }
        return (pairs, matches);
    }

    private async Task<string> RunOneAsync(
        PipelineContext pipeline, string repository, string key, ISandbox sandbox, ContextVerifyStages context,
        CancellationToken ct)
    {
        var notes = new VerifyResolutionNotes();
        var runnable = await resolver.ResolveAsync(key, map: null, sandbox, [context], notes, ct);
        var pair = $"{context.ContextName}/{context.Stages[0].Label}";
        if (runnable.Count == 0)
            return notes.Findings.Count > 0
                ? $"Error: {string.Join(" ", notes.Findings)} Nothing was run."
                : $"{pair} in {repository} is declared, not runnable: its when_present path is absent. Nothing was run.";

        var stage = runnable[0];
        var outcome = await runner.RunAsync(key, stage.Stage, sandbox, stage.Cwd, stage.Command, ct);
        var output = VerifyCommandRunner.Tail(outcome.Output, VerifyCommandRunner.OutputTailChars);
        // 2026-10-17-a196c: a stage the master ran is a command of this spec, citable for a build or test claim.
        Specs.SpecRunCommandScope.Open(pipeline).Record(repository, stage.Command, output, outcome.ExitCode);
        return $"{pair} '{stage.Command}' in {repository} exited {outcome.ExitCode}:\n{output}";
    }

    private static string Listing(IReadOnlyList<string> pairs) =>
        pairs.Count == 0 ? "It declares no stage." : $"Declared: {string.Join(", ", pairs.Distinct())}.";

    private static bool Same(string? left, string? right) =>
        string.Equals(left?.Trim(), right?.Trim(), StringComparison.OrdinalIgnoreCase);
}
