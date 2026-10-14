using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Tests.TestHelpers;
using Microsoft.Extensions.AI;

namespace AgentSmith.Tests.Sanity;

/// <summary>A pipeline carrying a current spec and a sandbox map, and a tool call made through the real wrapper.</summary>
internal static class SanityCalls
{
    public static PipelineContext Pipeline(
        IReadOnlyList<string>? evidence = null, IReadOnlyList<string>? repos = null,
        IReadOnlyDictionary<string, string>? map = null, IReadOnlyList<string>? targets = null, bool withSpec = true)
    {
        var pipeline = new PipelineContext();
        pipeline.Set<IReadOnlyDictionary<string, string>>(ContextKeys.SandboxRepos,
            map ?? new Dictionary<string, string> { ["default"] = "shop" });
        if (withSpec) pipeline.Set(ContextKeys.CurrentSpec, Spec(evidence, repos, targets));
        return pipeline;
    }

    public static SpecDraft Spec(
        IReadOnlyList<string>? evidence = null, IReadOnlyList<string>? repos = null, IReadOnlyList<string>? targets = null) =>
        new("2026-10-13-0000a", "goal", "spec: 2026-10-13-0000a", [])
        {
            Facts = [.. (evidence ?? []).Select(e => new SpecFact("a claim", e))],
            Repos = repos ?? [],
            Steps = [.. (targets ?? []).Select((t, i) => new SpecStep($"s{i}", "do it", t))],
        };

    /// <summary>Calls a tool named <paramref name="tool"/> answering <paramref name="result"/>, wrapped; returns what the model reads.</summary>
    public static async Task<object?> Call(
        PipelineContext pipeline, string tool, object? result, params (string Name, object? Value)[] arguments)
    {
        var wrapped = (AIFunction)TestSanity.Wrapper().Wrap([new FixedResultTool(tool, result)], pipeline).Single();
        var args = new AIFunctionArguments();
        foreach (var (name, value) in arguments) args[name] = value;
        return await wrapped.InvokeAsync(args, CancellationToken.None);
    }

    public static async Task<string> Text(
        PipelineContext pipeline, string tool, string result, params (string Name, object? Value)[] arguments) =>
        (string)(await Call(pipeline, tool, result, arguments))!;

    /// <summary>A tool of a given name that answers a fixed result, or throws one exception.</summary>
    internal sealed class FixedResultTool(string name, object? result, Exception? throws = null) : AIFunction
    {
        public override string Name => name;

        protected override ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken) =>
            throws is null ? ValueTask.FromResult(result) : throw throws;
    }
}
