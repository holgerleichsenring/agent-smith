using System.Text;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Application.Services.Handlers;

public sealed class KnowledgePromptBuilder(IPromptCatalog prompts)
{
    public string BuildSystemPrompt() => prompts.Get("knowledge-system");

    public string BuildUserPrompt(
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
