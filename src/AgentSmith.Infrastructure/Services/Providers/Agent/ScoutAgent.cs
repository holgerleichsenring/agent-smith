using AgentSmith.Infrastructure.Models;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using Anthropic.SDK;
using Anthropic.SDK.Messaging;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Providers.Agent;

/// <summary>
/// Lightweight file-discovery agent using a cheap model (Haiku).
/// Explores the codebase with read-only tools to identify relevant files
/// before the primary coding agent runs.
/// </summary>
public sealed class ScoutAgent(
    AnthropicClient client,
    string model,
    int maxTokens,
    ILogger logger,
    TokenUsageTracker? usageTracker = null,
    IProgressReporter? progressReporter = null)
{
    private const int MaxScoutIterations = 5;

    private const string ScoutSystemPrompt = """
        You are a codebase scout. Your job is to explore the repository and identify
        all files relevant to the implementation plan below.

        Instructions:
        - Use list_files to understand the project structure
        - Use read_file to examine files that might be relevant
        - Focus on files that will need to be created or modified
        - Also examine related files (imports, dependencies, tests)
        - When done, provide a brief summary of what you found and which files are relevant

        Do NOT attempt to modify any files. You only have read-only access.
        """;

    public async Task<ScoutResult> DiscoverAsync(
        Plan plan,
        string repositoryPath,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Scout agent starting file discovery with model {Model}", model);

        var fileReadTracker = new FileReadTracker();
        var toolExecutor = new ToolExecutor(
            repositoryPath, logger, fileReadTracker, progressReporter);
        var totalTokens = 0;

        var messages = new List<Message>
        {
            new()
            {
                Role = RoleType.User,
                Content = new List<ContentBase>
                {
                    new TextContent { Text = BuildScoutPrompt(plan, repositoryPath) }
                }
            }
        };

        string contextSummary = "";

        for (var iteration = 0; iteration < MaxScoutIterations; iteration++)
        {
            logger.LogDebug("Scout iteration {Iteration}", iteration + 1);

            var response = await client.Messages.GetClaudeMessageAsync(
                new MessageParameters
                {
                    Model = model,
                    MaxTokens = maxTokens,
                    System = new List<SystemMessage> { new(ScoutSystemPrompt) },
                    Messages = messages,
                    Tools = ToolDefinitions.ScoutTools,
                    Stream = false
                },
                cancellationToken);

            totalTokens += response.Usage.InputTokens + response.Usage.OutputTokens;
            usageTracker?.Track(response);

            messages.Add(new Message
            {
                Role = RoleType.Assistant,
                Content = response.Content
            });

            if (!response.Content.OfType<ToolUseContent>().Any())
            {
                contextSummary = response.Content
                    .OfType<TextContent>()
                    .Select(t => t.Text)
                    .FirstOrDefault() ?? "";

                logger.LogInformation(
                    "Scout completed after {Iterations} iterations, found {FileCount} files",
                    iteration + 1, fileReadTracker.GetAllReadFiles().Count);
                break;
            }

            var toolResults = new List<ToolResultContent>();
            foreach (var toolUse in response.Content.OfType<ToolUseContent>())
            {
                var result = await toolExecutor.ExecuteAsync(toolUse.Name, toolUse.Input);
                toolResults.Add(new ToolResultContent
                {
                    ToolUseId = toolUse.Id,
                    Content = new List<ContentBase> { new TextContent { Text = result } },
                    IsError = result.StartsWith("Error:", StringComparison.Ordinal)
                });
            }

            messages.Add(new Message
            {
                Role = RoleType.User,
                Content = toolResults.Cast<ContentBase>().ToList()
            });
        }

        var relevantFiles = fileReadTracker.GetAllReadFiles().ToList();

        return new ScoutResult(relevantFiles, contextSummary, totalTokens);
    }

    private static string BuildScoutPrompt(Plan plan, string repositoryPath)
    {
        var steps = string.Join('\n', plan.Steps.Select(
            s => $"  {s.Order}. [{s.ChangeType}] {s.Description} → {s.TargetFile}"));

        return $"""
            Explore the repository at: {repositoryPath}

            ## Implementation Plan
            **Summary:** {plan.Summary}

            **Steps:**
            {steps}

            Start by listing the root directory to understand the project structure,
            then read files that are relevant to these changes.
            """;
    }
}
