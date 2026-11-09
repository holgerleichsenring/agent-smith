using AgentSmith.Contracts.Commands;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-11-09-0620c: the last words of a master's user prompt, written for the master's role. The generic user prompt
/// served every master without a schema of its own, so a discussion master and a legal master ended on a coding
/// procedure their skills forbid. The coding closing is kept as it was.
/// </summary>
internal static class MasterClosing
{
    internal const string Coding =
        """
        Investigate the repositories, plan your change, implement it, and verify
        it (build + tests). Use the available tools — read_file, grep_in_tree,
        edit, write_file, run_command, log_decision, ask_human. When you are
        done, stop calling tools and summarise what changed.
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
