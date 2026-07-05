namespace AgentSmith.Application.Prompts;

/// <summary>
/// The closed vocabulary of template tokens a master prompt may reference (rendered by
/// <see cref="IPromptCatalog.Render"/>). This is the single source of truth used to detect
/// an UNBOUND known token — a <c>{ProjectContextSection}</c> etc. the caller forgot to
/// supply would otherwise reach the LLM verbatim. Braces outside this set (e.g. an OpenAPI
/// path example like <c>/users/{id}</c>) are intentionally left untouched.
/// </summary>
public static class MasterPromptTokens
{
    public static readonly IReadOnlyList<string> All =
    [
        "ProjectContextSection",
        "CodingPrinciples",
        "CodeMapSection",
        "RepoNames",
        "PlanSection",
        "RunRecordDir",
        "MaxFixIterations",
    ];
}
