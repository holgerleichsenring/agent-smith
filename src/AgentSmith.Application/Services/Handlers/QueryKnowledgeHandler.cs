using System.Text;
using AgentSmith.Application.Models;
using AgentSmith.Application.Services;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Queries the project knowledge base wiki to answer user questions.
/// Reads relevant wiki files and uses the LLM to synthesize an answer.
/// </summary>
public sealed class QueryKnowledgeHandler(
    IChatClientFactory chatClientFactory,
    ILogger<QueryKnowledgeHandler> logger)
    : ICommandHandler<QueryKnowledgeContext>
{
    private const string IndexFile = "index.md";

    public async Task<CommandResult> ExecuteAsync(
        QueryKnowledgeContext context, CancellationToken cancellationToken)
    {
        var wikiPath = context.WikiPath;

        if (!Directory.Exists(wikiPath) || !File.Exists(Path.Combine(wikiPath, IndexFile)))
        {
            logger.LogInformation("No wiki found at {WikiPath}", wikiPath);
            context.Pipeline.Set(ContextKeys.QueryAnswer, "No wiki found. Run compile-wiki first.");
            return CommandResult.Ok("No wiki found. Run compile-wiki first.");
        }

        var wikiContent = await ReadRelevantWikiFilesAsync(wikiPath, context.Question, cancellationToken);

        var systemPrompt = BuildSystemPrompt();
        var userPrompt = BuildUserPrompt(context.Question, wikiContent);

        logger.LogInformation("Querying knowledge base with: {Question}", context.Question);

        var chat = chatClientFactory.Create(context.AgentConfig, TaskType.Summarization);
        var maxTokens = chatClientFactory.GetMaxOutputTokens(context.AgentConfig, TaskType.Summarization);
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, systemPrompt),
            new(ChatRole.User, userPrompt),
        };
        var response = await chat.GetResponseAsync(messages,
            new ChatOptions { MaxOutputTokens = maxTokens }, cancellationToken);
        PipelineCostTracker.GetOrCreate(context.Pipeline).Track(response);

        var responseText = response.Text ?? string.Empty;
        var answer = FormatAnswer(responseText, wikiPath);
        context.Pipeline.Set(ContextKeys.QueryAnswer, answer);

        var outputTokens = response.Usage?.OutputTokenCount ?? 0;
        logger.LogInformation("Knowledge base query answered ({Tokens} tokens)", outputTokens);
        return CommandResult.Ok(answer);
    }

    internal static async Task<Dictionary<string, string>> ReadRelevantWikiFilesAsync(
        string wikiPath, string question, CancellationToken ct)
    {
        var result = new Dictionary<string, string>();
        var files = Directory.GetFiles(wikiPath, "*.md");
        var questionLower = question.ToLowerInvariant();

        // Always include index.md
        var indexPath = Path.Combine(wikiPath, IndexFile);
        if (File.Exists(indexPath))
            result[IndexFile] = await File.ReadAllTextAsync(indexPath, ct);

        // Include files whose name or content is likely relevant
        foreach (var file in files)
        {
            var fileName = Path.GetFileName(file);
            if (fileName == IndexFile)
                continue;

            // Always include core wiki files
            if (IsCorefile(fileName) || FileNameMatchesQuestion(fileName, questionLower))
            {
                result[fileName] = await File.ReadAllTextAsync(file, ct);
            }
        }

        return result;
    }

    private static readonly HashSet<string> DefaultCoreFiles =
        ["decisions.md", "known-issues.md", "patterns.md"];

    internal static HashSet<string> CoreFiles { get; set; } = DefaultCoreFiles;

    private static bool IsCorefile(string fileName) =>
        CoreFiles.Contains(fileName);

    private static bool FileNameMatchesQuestion(string fileName, string questionLower)
    {
        var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName).ToLowerInvariant();
        var keywords = nameWithoutExt.Split('-', '_');
        return keywords.Any(k => k.Length > 2 && questionLower.Contains(k));
    }

    private static string BuildSystemPrompt() =>
        """
        You are a project knowledge assistant. Answer the user's question based on the provided wiki content.
        Be concise and specific. Reference the source wiki file when possible.
        If the wiki doesn't contain relevant information, say so clearly.
        All text must be in English.
        """;

    internal static string BuildUserPrompt(string question, Dictionary<string, string> wikiContent)
    {
        var sb = new StringBuilder();
        sb.AppendLine("## Question");
        sb.AppendLine(question);
        sb.AppendLine();
        sb.AppendLine("## Wiki Content");
        sb.AppendLine();

        foreach (var (fileName, content) in wikiContent)
        {
            sb.AppendLine($"### {fileName}");
            sb.AppendLine(content);
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static string FormatAnswer(string llmAnswer, string wikiPath)
    {
        var sb = new StringBuilder();
        sb.AppendLine(llmAnswer.Trim());
        sb.AppendLine();
        sb.AppendLine($"*Source: {wikiPath}*");
        return sb.ToString();
    }
}
