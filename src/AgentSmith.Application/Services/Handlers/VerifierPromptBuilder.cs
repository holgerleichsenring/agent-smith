namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// p0129a: builds the (system, user) prompt pair fed to a VerifyDiff investigator.
/// The skill body is the system prompt; the user message carries the persisted Plan
/// and Diff JSON plus the verbatim observation-emission rule.
/// </summary>
internal static class VerifierPromptBuilder
{
    private const string EmitInstruction =
        "Emit observations as a single-line JSON array per observation-schema.md. " +
        "No prose, no markdown fences. Use blocking=true only for high-confidence violations " +
        "of the rule the skill body describes.";

    public static (string System, string User) Build(
        string skillBody, string planJson, string diffJson, string? codingPrinciples = null)
    {
        var system = $"{skillBody.Trim()}\n\n{EmitInstruction}";
        var user = BuildUserMessage(planJson, diffJson, codingPrinciples);
        return (system, user);
    }

    private static string BuildUserMessage(string planJson, string diffJson, string? codingPrinciples)
    {
        var planSection = string.IsNullOrWhiteSpace(planJson) ? "(no plan)" : planJson.Trim();
        var diffSection = string.IsNullOrWhiteSpace(diffJson) ? "(no diff)" : diffJson.Trim();
        var principlesSection = string.IsNullOrWhiteSpace(codingPrinciples)
            ? string.Empty
            : $"""

                ## Coding principles
                ```markdown
                {codingPrinciples.Trim()}
                ```

                """;
        return $"""
            ## Plan
            ```json
            {planSection}
            ```
            {principlesSection}
            ## Diff
            ```json
            {diffSection}
            ```

            Verify the Diff against the Plan per the system-prompt rule. Emit observations.
            """;
    }
}
