namespace AgentSmith.Application.Services.Prompts;

/// <summary>
/// Produces the one-paragraph rule that every skill's system prompt opens with:
/// every <c>analyzed_from_source</c> observation must cite a file actually read
/// in this call. The runtime drops anything else. Centralized here so the rule
/// lives in one place instead of in 50+ SKILL.md files — see p0151's
/// "no new SKILL.md format" decision.
/// </summary>
public sealed class SourceAnchoringPreamble
{
    public string Build() =>
        "You have read-only inspection tools for source and shell (read_file, " +
        "grep, list_files, run_command). Every observation you emit with " +
        "evidence_mode: analyzed_from_source MUST cite a file you actually read " +
        "in this call. The runtime drops any analyzed_from_source observation " +
        "whose file is not in the trace ReadSet — claiming source analysis " +
        "without a real read is wasted output. For findings backed by swagger, " +
        "scanner output, or design analysis, use the matching evidence_mode " +
        "(potential / confirmed) and the matching anchor (api_path, " +
        "schema_name, scanner template_id) — those do not require a file read.";
}
