using AgentSmith.Contracts.Commands;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-11-09-0620c: the last words of a master's user prompt, written for the master's role. The generic user prompt
/// served every master without a schema of its own, so a discussion master and a legal master ended on a coding
/// procedure their skills forbid. 2026-11-09-0620f: the coding closing ends on the verdict block the framework parses,
/// not on a summary, and leaves the tools to describe themselves.
/// </summary>
internal static class MasterClosing
{
    internal const string Coding =
        """
        Investigate the repositories, plan your change, implement it, and verify
        it (build + tests). Your last message is your verdict block, which the
        framework reads to decide what this run delivered.
        """;

    internal const string Report =
        "The topic is the ticket above. What you deliver is the report your methodology names — nothing else you "
        + "write is delivered, and nothing in the repositories needs to change.";

    internal static string For(string master, PipelineContext pipeline) =>
        master == PipelinePresets.MasterFor("mad-discussion") ? Report
        : master == PipelinePresets.MasterFor("legal-analysis") ? Legal(pipeline)
        : Coding;

    private static string Legal(PipelineContext pipeline)
    {
        var path = pipeline.TryGet<string>(ContextKeys.DocumentMarkdownPath, out var p) && !string.IsNullOrWhiteSpace(p)
            ? $"The document is converted to markdown at `{p}`"
            : "The document is converted to markdown beside the original in the workspace";
        var type = pipeline.TryGet<string>(ContextKeys.ContractType, out var t) && !string.IsNullOrWhiteSpace(t)
            ? $"; a first reading classified it as `{t}` — confirm or correct that in the report"
            : string.Empty;
        return $"{path}{type}. What you deliver is the report your methodology names — nothing else you write is "
            + "delivered.";
    }
}
