using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-10-13-a347a: the declared formatters of ONE repository that failed at a spec commit, at the
/// top of that repository's pull-request body — right after the banner, because Azure DevOps cuts a
/// long description from the end. Each names its command, exit code and output tail, and the lever
/// when the command itself is wrong: the context's 'format' in its context.yaml.
/// </summary>
public static class SpecFormatFailureSection
{
    public const string Heading = "## Formatter failed";

    private const string Preamble =
        "A formatter a context declares failed at the spec commit; its files are committed as written.";

    public static string Build(PipelineContext pipeline, string? repository)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        if (repository is null
            || !pipeline.TryGet<IReadOnlyList<SpecFormatFailure>>(ContextKeys.SpecFormatFailures, out var all)
            || all is null)
            return string.Empty;
        var own = all.Where(f => f.Repository == repository).ToList();
        if (own.Count == 0) return string.Empty;
        var lines = new List<string> { Heading, string.Empty, Preamble, string.Empty };
        foreach (var failure in own) lines.AddRange(Entry(failure));
        return string.Join("\n", lines) + "\n";
    }

    private static IEnumerable<string> Entry(SpecFormatFailure failure)
    {
        var fence = Fence(failure.OutputTail);
        yield return $"- `{failure.ContextName}`: `{failure.DeclaredCommand}` exited {failure.ExitCode}";
        yield return string.Empty;
        yield return fence;
        yield return failure.OutputTail.Length == 0 ? "(no output)" : failure.OutputTail;
        yield return fence;
        yield return string.Empty;
        yield return "If the command itself is wrong, correct `format` in "
            + $"`.agentsmith/contexts/{failure.ContextName}/context.yaml` to the rewriting form the repository's CI uses.";
        yield return string.Empty;
    }

    // A fence longer than any backtick run in the output, so the output cannot close it.
    private static string Fence(string text)
    {
        int longest = 0, run = 0;
        foreach (var c in text)
        {
            run = c == '`' ? run + 1 : 0;
            longest = Math.Max(longest, run);
        }
        return new string('`', Math.Max(3, longest + 1));
    }
}
