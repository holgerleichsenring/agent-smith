using System.Text;
using AgentSmith.Application.Models;
using AgentSmith.Application.Services;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Queries the project knowledge base wiki to answer user questions.
/// Reads relevant wiki files via the sandbox and uses the LLM to synthesize an answer.
/// </summary>
public sealed class QueryKnowledgeHandler(
    IChatClientFactory chatClientFactory,
    ISandboxFileReaderFactory readerFactory,
    ILogger<QueryKnowledgeHandler> logger)
    : ICommandHandler<QueryKnowledgeContext>
{
    private const string IndexFile = "index.md";

    public async Task<CommandResult> ExecuteAsync(
        QueryKnowledgeContext context, CancellationToken cancellationToken)
    {
        var sandbox = context.Pipeline.Get<ISandbox>(ContextKeys.Sandbox);
        var reader = readerFactory.Create(sandbox);
        var wikiPath = context.WikiPath;

        var indexContent = await reader.TryReadAsync(Path.Combine(wikiPath, IndexFile), cancellationToken);
        if (indexContent is null)
        {
            logger.LogInformation("No wiki found at {WikiPath}", wikiPath);
            context.Pipeline.Set(ContextKeys.QueryAnswer, "No wiki found. Run compile-wiki first.");
            return CommandResult.Ok("No wiki found. Run compile-wiki first.");
        }

        var wikiContent = await ReadRelevantWikiFilesAsync(reader, wikiPath, context.Question, indexContent, cancellationToken);

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
        ISandboxFileReader reader, string wikiPath, string question, string indexContent, CancellationToken ct)
    {
        var result = new Dictionary<string, string> { [IndexFile] = indexContent };
        var entries = await reader.ListAsync(wikiPath, maxDepth: 1, ct);
        var mdFiles = entries.Where(e => e.EndsWith(".md", StringComparison.OrdinalIgnoreCase));
        var questionLower = question.ToLowerInvariant();

        foreach (var file in mdFiles)
        {
            var fileName = LastSegment(file);
            if (fileName == IndexFile) continue;

            if (IsCorefile(fileName) || FileNameMatchesQuestion(fileName, questionLower))
            {
                var content = await reader.TryReadAsync(file, ct);
                if (content is not null) result[fileName] = content;
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

    private static string LastSegment(string path)
    {
        var idx = path.LastIndexOf('/');
        return idx < 0 ? path : path[(idx + 1)..];
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
