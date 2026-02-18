using System.Text.Json;
using System.Text.Json.Nodes;
using AgentSmith.Contracts.Configuration;
using AgentSmith.Domain.Entities;
using GenerativeAI;
using GenerativeAI.Types;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Providers.Agent;

/// <summary>
/// Runs the agentic tool-calling loop against the Google Gemini API.
/// Uses GenerativeModel.GenerateContentAsync with manual conversation management.
/// </summary>
public sealed class GeminiAgenticLoop(
    GenerativeModel model,
    ToolExecutor toolExecutor,
    ILogger logger,
    TokenUsageTracker tracker,
    int maxIterations = 25)
{
    public async Task<IReadOnlyList<CodeChange>> RunAsync(
        string systemPrompt,
        string userMessage,
        CancellationToken cancellationToken = default)
    {
        var contents = new List<Content>
        {
            new(userMessage, Roles.User)
        };

        for (var iteration = 0; iteration < maxIterations; iteration++)
        {
            logger.LogDebug("Gemini agentic loop iteration {Iteration}", iteration + 1);

            var request = new GenerateContentRequest
            {
                SystemInstruction = new Content(systemPrompt, Roles.User),
                Contents = contents,
                Tools = new List<Tool> { GeminiToolDefinitions.AllTools }
            };

            var response = await model.GenerateContentAsync(request, cancellationToken: cancellationToken);
            TrackUsage(response, iteration + 1);

            var candidate = response?.Candidates?.FirstOrDefault();
            if (candidate?.Content is null)
            {
                logger.LogInformation("Gemini returned no content at iteration {Iteration}", iteration + 1);
                break;
            }

            contents.Add(candidate.Content);

            var functionCalls = candidate.Content.Parts?
                .Where(p => p.FunctionCall is not null)
                .Select(p => p.FunctionCall!)
                .ToList();

            if (functionCalls is null || functionCalls.Count == 0)
            {
                logger.LogInformation(
                    "Gemini agent completed after {Iterations} iterations", iteration + 1);
                break;
            }

            var responseParts = await ProcessFunctionCallsAsync(functionCalls, cancellationToken);
            contents.Add(new Content(responseParts, Roles.Function));
        }

        tracker.LogSummary(logger);
        return toolExecutor.GetChanges();
    }

    private async Task<List<Part>> ProcessFunctionCallsAsync(
        List<FunctionCall> functionCalls, CancellationToken cancellationToken)
    {
        var parts = new List<Part>();

        foreach (var call in functionCalls)
        {
            logger.LogDebug("Executing tool: {Tool}", call.Name);

            JsonNode? input = null;
            if (call.Args is not null)
            {
                var argsJson = JsonSerializer.Serialize(call.Args);
                input = JsonNode.Parse(argsJson);
            }

            var toolResult = await toolExecutor.ExecuteAsync(call.Name, input);

            parts.Add(new Part
            {
                FunctionResponse = new FunctionResponse
                {
                    Name = call.Name,
                    Response = JsonSerializer.SerializeToNode(new { result = toolResult })
                }
            });
        }

        return parts;
    }

    private void TrackUsage(GenerateContentResponse? response, int iteration)
    {
        if (response?.UsageMetadata is null) return;

        var usage = response.UsageMetadata;
        var inputTokens = usage.PromptTokenCount;
        var outputTokens = usage.CandidatesTokenCount;

        tracker.Track(inputTokens, outputTokens);

        logger.LogDebug(
            "Gemini iteration {Iteration} tokens: Input={Input}, Output={Output}",
            iteration, inputTokens, outputTokens);
    }
}
