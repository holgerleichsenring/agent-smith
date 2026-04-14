using System.Text;

namespace AgentSmith.Application.Services.Handlers;

internal static class KnowledgePromptBuilder
{
    public static string BuildSystemPrompt() =>
        """
        You are a technical writer compiling a project knowledge base from AI agent run history.
        Your output must be a JSON object with a single key "wiki_updates" containing filename-content pairs.
        Each file should be valid Markdown. Create or update these files as needed:
        - index.md: Table of contents linking to all wiki pages
        - decisions.md: Architectural and design decisions made across runs
        - known-issues.md: Known bugs, limitations, and workarounds discovered
        - patterns.md: Coding patterns, conventions, and best practices established
        - Additional concept articles as warranted by the content

        Rules:
        - Synthesize information, don't just copy run data
        - Group related decisions together
        - Note when a later run supersedes an earlier decision
        - Use clear headings and bullet points
        - All text must be in English
        - Output ONLY valid JSON, no markdown fences or other text
        """;

    public static string BuildUserPrompt(
        string existingWiki, List<RunDirectoryReader.RunData> runs)
    {
        var sb = new StringBuilder();

        if (!string.IsNullOrWhiteSpace(existingWiki))
        {
            sb.AppendLine("## Existing Wiki (index.md)");
            sb.AppendLine(existingWiki);
            sb.AppendLine();
        }

        sb.AppendLine("## New Run Data");
        sb.AppendLine();

        foreach (var run in runs)
        {
            sb.AppendLine($"### Run r{run.RunNumber:D2} ({run.DirName})");
            sb.AppendLine();

            if (!string.IsNullOrWhiteSpace(run.Plan))
            {
                sb.AppendLine("#### Plan");
                sb.AppendLine(run.Plan);
                sb.AppendLine();
            }

            if (!string.IsNullOrWhiteSpace(run.Result))
            {
                sb.AppendLine("#### Result");
                sb.AppendLine(run.Result);
                sb.AppendLine();
            }
        }

        sb.AppendLine("Please compile the above into wiki pages. Output JSON: { \"wiki_updates\": { \"filename.md\": \"content\" } }");
        return sb.ToString();
    }
}
