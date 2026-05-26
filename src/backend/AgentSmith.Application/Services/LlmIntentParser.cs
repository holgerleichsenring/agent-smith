using System.Text.Json;
using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services;

/// <summary>
/// Parses free-text user input (Slack, Teams) into a PipelineRequest via LLM.
/// Uses the configured Reasoning task type (typically Haiku) for cheap intent
/// classification. Also implements IIntentParser for backward compatibility.
/// </summary>
public sealed class LlmIntentParser(
    IChatClientFactory chatClientFactory,
    IConfigurationLoader configLoader,
    AgentConfig haikusConfig,
    ILogger<LlmIntentParser> logger) : IIntentParser
{
    private const string SystemPrompt = """
        You are an intent parser for Agent Smith, an AI orchestration framework.
        Parse the user's message into a structured command.

        Respond with valid JSON only, no markdown, no explanation.
        {
          "pipeline": "fix-bug|add-feature|init-project|security-scan|api-security-scan|legal-analysis|mad-discussion",
          "project": "project-name",
          "ticket_id": "123 or null if not applicable",
          "confidence": 0.0-1.0
        }

        If you cannot determine the pipeline or project, set confidence to 0.
        """;

    public async Task<PipelineRequest> ParseToPipelineRequestAsync(
        string userInput,
        string configPath,
        CancellationToken cancellationToken)
    {
        var config = configLoader.LoadConfig(configPath);
        var projectNames = string.Join(", ", config.Projects.Keys);
        var pipelineNames = string.Join(", ", PipelinePresets.Names);

        var userPrompt = $"""
            Available projects: {projectNames}
            Available pipelines: {pipelineNames}

            User message: {userInput}
            """;

        var chat = chatClientFactory.Create(haikusConfig, TaskType.Reasoning);
        var maxTokens = chatClientFactory.GetMaxOutputTokens(haikusConfig, TaskType.Reasoning);
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, SystemPrompt),
            new(ChatRole.User, userPrompt),
        };
        var response = await chat.GetResponseAsync(messages,
            new ChatOptions { MaxOutputTokens = maxTokens }, cancellationToken);

        return ParseResponse(response.Text ?? string.Empty, userInput);
    }

    /// <summary>
    /// Legacy IIntentParser implementation. Extracts ticket + project only.
    /// </summary>
    public async Task<ParsedIntent> ParseAsync(
        string userInput, CancellationToken cancellationToken)
    {
        var request = await ParseToPipelineRequestAsync(userInput, "config/agentsmith.yml", cancellationToken);

        var ticketId = request.TicketId ?? throw new ConfigurationException(
            $"Could not extract ticket ID from input: '{userInput}'");

        return new ParsedIntent(ticketId, new ProjectName(request.ProjectName));
    }

    private PipelineRequest ParseResponse(string response, string originalInput)
    {
        try
        {
            var json = response.Trim();
            var start = json.IndexOf('{');
            var end = json.LastIndexOf('}');
            if (start >= 0 && end > start)
                json = json[start..(end + 1)];

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var pipeline = root.GetProperty("pipeline").GetString() ?? "fix-bug";
            var project = root.GetProperty("project").GetString() ?? "";
            var ticketStr = root.TryGetProperty("ticket_id", out var tid) ? tid.GetString() : null;
            var confidence = root.TryGetProperty("confidence", out var c) ? c.GetDouble() : 0;

            if (confidence < 0.5)
            {
                logger.LogWarning("Low confidence ({Confidence}) parsing '{Input}'", confidence, originalInput);
            }

            var ticketId = !string.IsNullOrWhiteSpace(ticketStr) && ticketStr != "null"
                ? new TicketId(ticketStr) : null;

            logger.LogInformation(
                "LLM intent: pipeline={Pipeline}, project={Project}, ticket={Ticket}, confidence={Confidence}",
                pipeline, project, ticketStr ?? "none", confidence);

            return new PipelineRequest(project, pipeline, ticketId, Headless: true);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to parse LLM intent response, falling back to fix-bug");
            throw new ConfigurationException(
                $"Could not parse intent from '{originalInput}'. LLM response was not valid JSON.");
        }
    }
}
