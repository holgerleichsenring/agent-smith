namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Composes a skill-round user message into a stable prefix block and a per-skill
/// suffix block. The prefix carries the cache_control marker on Anthropic; on
/// providers without prompt caching the two halves are joined transparently.
/// </summary>
public sealed class PromptPrefixBuilder
{
    public (string Prefix, string Suffix) BuildDiscussionParts(
        string domainStable, string domainVariable,
        string? projectContext, string? domainRules, string? codeMap,
        string discussionSoFar, int round)
    {
        var prefix = $"""
            {domainStable}

            ## Project Context
            {projectContext ?? "Not available"}

            ## Domain Rules
            {domainRules ?? "Not available"}

            ## Code Map
            {codeMap ?? "Not available"}
            """.Trim();

        var suffix = $"""
            {(string.IsNullOrEmpty(domainVariable) ? "" : domainVariable + "\n\n")}## Discussion So Far
            {discussionSoFar}

            ## Your Task
            Based on the discussion so far, provide your analysis as a JSON array of observations.
            This is round {round}.

            Respond ONLY with a JSON array. No other text.
            """.Trim();
        return (prefix, suffix);
    }

    public (string Prefix, string Suffix) BuildStructuredParts(
        string domainStable, string domainVariable,
        string upstreamContext, string outputInstruction)
    {
        var suffix = $"""
            {(string.IsNullOrEmpty(domainVariable) ? "" : domainVariable + "\n\n")}{(string.IsNullOrEmpty(upstreamContext) ? "" : $"## Upstream Analysis\n{upstreamContext}\n\n")}## Output Format
            {outputInstruction}
            """.Trim();
        return (domainStable.Trim(), suffix);
    }
}
