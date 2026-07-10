namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Composes a skill-round user message into a stable prefix block and a per-skill
/// suffix block. p0323: the split is a prefix-STABILITY aid only — the two halves
/// are joined into one string by SkillPromptBuilder, and no per-block cache_control
/// marker exists on this path. Anthropic caching rides the automatic
/// tools-and-system directive set by ClaudeChatClientBuilder instead; keeping the
/// stable half first maximises the shared prefix for OpenAI's automatic caching.
/// </summary>
public sealed class PromptPrefixBuilder
{
    public (string Prefix, string Suffix) BuildDiscussionParts(
        string domainStable, string domainVariable,
        string? projectContext, string? domainRules, string? codeMap, string? existingTests,
        string discussionSoFar, int round)
    {
        var existingTestsBlock = string.IsNullOrEmpty(existingTests)
            ? ""
            : $"\n\n## Existing Tests\n{existingTests}";

        var prefix = $"""
            {domainStable}

            ## Project Context
            {projectContext ?? "Not available"}

            ## Domain Rules
            {domainRules ?? "Not available"}

            ## Code Map
            {codeMap ?? "Not available"}{existingTestsBlock}
            """.Trim();

        var suffix = $"""
            {(string.IsNullOrEmpty(domainVariable) ? "" : domainVariable + "\n\n")}## Discussion So Far
            {discussionSoFar}

            ## Your Task
            Investigate the discussion above and ground your analysis in the codebase. Use the available tools to read relevant source files and (when applicable) probe the running target before forming conclusions.
            This is round {round}.

            When your investigation is complete, respond with a JSON array of observations. The final response (after any tool calls) must be only the JSON array — no preamble or commentary outside the array.
            """.Trim();
        return (prefix, suffix);
    }

    public (string Prefix, string Suffix) BuildStructuredParts(
        string domainStable, string domainVariable,
        string upstreamContext, string outputInstruction,
        string? existingTests = null)
    {
        var existingTestsBlock = string.IsNullOrEmpty(existingTests)
            ? ""
            : $"## Existing Tests\n{existingTests}\n\n";

        var suffix = $"""
            {(string.IsNullOrEmpty(domainVariable) ? "" : domainVariable + "\n\n")}{existingTestsBlock}{(string.IsNullOrEmpty(upstreamContext) ? "" : $"## Upstream Analysis\n{upstreamContext}\n\n")}## Output Format
            {outputInstruction}
            """.Trim();
        return (domainStable.Trim(), suffix);
    }
}
