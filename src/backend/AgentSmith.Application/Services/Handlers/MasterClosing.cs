using AgentSmith.Contracts.Commands;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-11-09-0620c: the last words of a master's user prompt, written for the master's role. The generic user prompt
/// served every master without a schema of its own, so a discussion master and a legal master ended on a coding
/// procedure their skills forbid. 2026-11-09-0620k: the coding closing names only what the framework parses; the
/// method is the skill's.
/// </summary>
internal static class MasterClosing
{
    // 2026-11-09-0620k: no procedure — the skill carries the method. What the server adds is what it parses.
    internal const string Coding =
        "The ticket above is the work. The framework parses your last message: your verdict block, or, when you "
        + "asked with ask_human, the question.";

    internal const string Report =
        "The topic is the ticket above. What you deliver is the report your methodology names, written where it says; "
        + "the code itself does not change.";

    internal static string For(string master, PipelineContext pipeline) =>
        master == PipelinePresets.MasterFor("mad-discussion") ? Report
        : master == PipelinePresets.MasterFor("legal-analysis") ? Legal(pipeline)
        : Coding;

    private static string Legal(PipelineContext pipeline)
    {
        var path = pipeline.TryGet<string>(ContextKeys.DocumentMarkdownPath, out var p) && !string.IsNullOrWhiteSpace(p)
            ? $"The document is converted to markdown at `{p}`, from the repository root"
            : "The document is converted to markdown beside the original in the workspace";
        var type = pipeline.TryGet<string>(ContextKeys.ContractType, out var t) && !string.IsNullOrWhiteSpace(t)
            ? $"; a first reading classified it as `{t}` — confirm or correct that in the report"
            : string.Empty;
        return $"{path}{type}. What you deliver is the report your methodology names — nothing else you write is "
            + "delivered.";
    }
}
