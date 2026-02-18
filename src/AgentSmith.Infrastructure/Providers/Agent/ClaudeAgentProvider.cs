using System.Text.Json;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Domain.ValueObjects;
using Anthropic.SDK;
using Anthropic.SDK.Messaging;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Providers.Agent;

/// <summary>
/// AI agent provider using the Anthropic Claude API.
/// Supports plan generation (single call) and agentic execution (tool-calling loop).
/// </summary>
public sealed class ClaudeAgentProvider(
    string apiKey,
    string model,
    ILogger<ClaudeAgentProvider> logger) : IAgentProvider
{
    public string ProviderType => "Claude";

    public async Task<Plan> GeneratePlanAsync(
        Ticket ticket,
        CodeAnalysis codeAnalysis,
        string codingPrinciples,
        CancellationToken cancellationToken = default)
    {
        using var client = new AnthropicClient(apiKey);

        var systemPrompt = BuildPlanSystemPrompt(codingPrinciples);
        var userPrompt = BuildPlanUserPrompt(ticket, codeAnalysis);

        var response = await client.Messages.GetClaudeMessageAsync(
            new MessageParameters
            {
                Model = model,
                MaxTokens = 4096,
                System = new List<SystemMessage> { new(systemPrompt) },
                Messages = new List<Message>
                {
                    new()
                    {
                        Role = RoleType.User,
                        Content = new List<ContentBase> { new TextContent { Text = userPrompt } }
                    }
                },
                Stream = false
            },
            cancellationToken);

        var rawResponse = ExtractTextResponse(response);
        return ParsePlan(rawResponse);
    }

    public async Task<IReadOnlyList<CodeChange>> ExecutePlanAsync(
        Plan plan,
        Repository repository,
        string codingPrinciples,
        CancellationToken cancellationToken = default)
    {
        using var client = new AnthropicClient(apiKey);

        var toolExecutor = new ToolExecutor(repository.LocalPath, logger);
        var loop = new AgenticLoop(client, model, toolExecutor, logger);

        var systemPrompt = BuildExecutionSystemPrompt(codingPrinciples);
        var userMessage = BuildExecutionUserPrompt(plan, repository);

        var changes = await loop.RunAsync(systemPrompt, userMessage, cancellationToken);

        logger.LogInformation(
            "Agentic execution completed with {Count} file changes", changes.Count);

        return changes;
    }

    private static string BuildPlanSystemPrompt(string codingPrinciples)
    {
        return $$"""
            You are a senior software engineer. Analyze the following ticket and codebase,
            then create a detailed implementation plan.

            ## Coding Principles
            {{codingPrinciples}}

            ## Respond in JSON format:
            {
              "summary": "Brief summary of what needs to be done",
              "steps": [
                { "order": 1, "description": "...", "target_file": "...", "change_type": "Create|Modify|Delete" }
              ]
            }

            Respond ONLY with the JSON, no additional text.
            """;
    }

    private static string BuildPlanUserPrompt(Ticket ticket, CodeAnalysis codeAnalysis)
    {
        var files = string.Join('\n', codeAnalysis.FileStructure.Take(200));
        var deps = string.Join('\n', codeAnalysis.Dependencies);

        return $"""
            ## Ticket
            **ID:** {ticket.Id}
            **Title:** {ticket.Title}
            **Description:** {ticket.Description}
            **Acceptance Criteria:** {ticket.AcceptanceCriteria ?? "None specified"}

            ## Codebase Analysis
            **Language:** {codeAnalysis.Language ?? "Unknown"}
            **Framework:** {codeAnalysis.Framework ?? "Unknown"}

            ### Dependencies
            {deps}

            ### File Structure
            {files}
            """;
    }

    private static string BuildExecutionSystemPrompt(string codingPrinciples)
    {
        return $"""
            You are a senior software engineer implementing code changes.
            You have access to tools to read, write, and list files in the repository,
            as well as run shell commands.

            ## Coding Principles
            {codingPrinciples}

            ## Instructions
            - Read existing files before modifying them to understand the current state.
            - Write complete file contents when using write_file (not diffs).
            - Follow the coding principles strictly.
            - Run build/test commands to verify your changes compile.
            - When done, stop calling tools and summarize what you did.
            """;
    }

    private static string BuildExecutionUserPrompt(Plan plan, Repository repository)
    {
        var steps = string.Join('\n', plan.Steps.Select(
            s => $"  {s.Order}. [{s.ChangeType}] {s.Description} → {s.TargetFile}"));

        return $"""
            Execute the following implementation plan in repository at: {repository.LocalPath}
            Branch: {repository.CurrentBranch}

            ## Plan
            **Summary:** {plan.Summary}

            **Steps:**
            {steps}

            Start by listing the relevant files, then implement each step.
            """;
    }

    private static string ExtractTextResponse(MessageResponse response)
    {
        var textContent = response.Content.OfType<TextContent>().FirstOrDefault();
        return textContent?.Text
            ?? throw new ProviderException("Claude", "Claude returned no text response.");
    }

    private static Plan ParsePlan(string rawJson)
    {
        try
        {
            var cleaned = StripMarkdownCodeBlock(rawJson);
            using var doc = JsonDocument.Parse(cleaned);
            var root = doc.RootElement;

            var summary = root.GetProperty("summary").GetString() ?? "";
            var steps = root.GetProperty("steps").EnumerateArray()
                .Select(ParsePlanStep)
                .ToList();

            return new Plan(summary, steps, rawJson);
        }
        catch (JsonException ex)
        {
            throw new ProviderException(
                "Claude", $"Failed to parse plan response from Claude: {ex.Message}", ex);
        }
    }

    private static string StripMarkdownCodeBlock(string text)
    {
        var trimmed = text.Trim();
        if (trimmed.StartsWith("```"))
        {
            var firstNewline = trimmed.IndexOf('\n');
            if (firstNewline >= 0)
                trimmed = trimmed[(firstNewline + 1)..];
        }
        if (trimmed.EndsWith("```"))
        {
            trimmed = trimmed[..^3].TrimEnd();
        }
        return trimmed;
    }

    private static PlanStep ParsePlanStep(JsonElement element)
    {
        var order = element.GetProperty("order").GetInt32();
        var description = element.GetProperty("description").GetString() ?? "";
        var targetFile = element.TryGetProperty("target_file", out var tf)
            ? new FilePath(tf.GetString()!)
            : null;
        var changeType = element.TryGetProperty("change_type", out var ct)
            ? ct.GetString() ?? "Modify"
            : "Modify";

        return new PlanStep(order, description, targetFile, changeType);
    }
}
