using System.Text.Json;
using System.Text.Json.Nodes;
using AgentSmith.Contracts.Services;
using GenerativeAI;
using GenerativeAI.Types;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Providers.Agent.Adapters;

/// <summary>
/// IAgenticAnalyzer implementation backed by the Google Gemini API.
/// Translates ToolDefinition to FunctionDeclaration and back; uses
/// GenerateContentAsync with manual conversation management.
/// </summary>
public sealed class GeminiAgenticAnalyzer(
    string apiKey,
    string model,
    ILogger<GeminiAgenticAnalyzer> logger,
    Func<string, GenerativeModel>? modelFactory = null) : IAgenticAnalyzer
{
    public async Task<AnalysisResult> AnalyzeAsync(
        string systemPrompt, string userPrompt,
        IReadOnlyList<ToolDefinition> tools, IToolCallHandler handler,
        int maxIterations, CancellationToken cancellationToken)
    {
        var generativeModel = (modelFactory ?? DefaultModelFactory)(model);
        var geminiTools = new List<Tool>
        {
            new() { FunctionDeclarations = tools.Select(ToFunctionDeclaration).ToList() }
        };

        var contents = new List<Content>
        {
            new(userPrompt, Roles.User)
        };

        var totalIn = 0; var totalOut = 0; var toolCallCount = 0;
        var iteration = 0;
        var finalText = string.Empty;

        for (; iteration < maxIterations; iteration++)
        {
            var request = new GenerateContentRequest
            {
                SystemInstruction = new Content(systemPrompt, Roles.User),
                Contents = contents,
                Tools = geminiTools
            };

            var response = await generativeModel.GenerateContentAsync(
                request, cancellationToken: cancellationToken);

            if (response?.UsageMetadata is { } usage)
            {
                totalIn += usage.PromptTokenCount;
                totalOut += usage.CandidatesTokenCount;
            }

            var candidate = response?.Candidates?.FirstOrDefault();
            if (candidate?.Content is null) { iteration++; break; }
            contents.Add(candidate.Content);

            var functionCalls = candidate.Content.Parts?
                .Where(p => p.FunctionCall is not null)
                .Select(p => p.FunctionCall!)
                .ToList();

            if (functionCalls is null || functionCalls.Count == 0)
            {
                finalText = candidate.Content.Parts?
                    .Where(p => !string.IsNullOrEmpty(p.Text))
                    .Select(p => p.Text!)
                    .FirstOrDefault()?.Trim() ?? string.Empty;
                iteration++;
                break;
            }

            var responseParts = new List<Part>();
            foreach (var call in functionCalls)
            {
                toolCallCount++;
                JsonNode? input = null;
                if (call.Args is not null)
                {
                    var argsJson = JsonSerializer.Serialize(call.Args);
                    input = JsonNode.Parse(argsJson);
                }
                // Gemini doesn't surface a stable per-call ID; use the function name + index.
                var callId = $"{call.Name}-{toolCallCount}";
                var result = await handler.HandleAsync(
                    new ToolCall(callId, call.Name, input), cancellationToken);
                responseParts.Add(new Part
                {
                    FunctionResponse = new FunctionResponse
                    {
                        Name = call.Name,
                        Response = JsonSerializer.SerializeToNode(new { result = result.Content })
                    }
                });
            }
            contents.Add(new Content(responseParts, Roles.Function));
        }

        logger.LogDebug(
            "Gemini analyzer: {Iterations} iterations, {ToolCalls} tool calls, {In}+{Out} tokens",
            iteration, toolCallCount, totalIn, totalOut);

        return new AnalysisResult(
            finalText, iteration, toolCallCount,
            new AnalyzerTokenUsage(totalIn, totalOut));
    }

    private static FunctionDeclaration ToFunctionDeclaration(ToolDefinition def) =>
        new()
        {
            Name = def.Name,
            Description = def.Description,
            Parameters = ToSchema(def.InputSchema)
        };

    private static Schema ToSchema(JsonObject obj)
    {
        // Translate the JSON-Schema-style input definition into Gemini's Schema record.
        var schema = new Schema
        {
            Type = (obj["type"]?.GetValue<string>() ?? "object").ToUpperInvariant()
        };

        if (obj["properties"] is JsonObject props)
        {
            schema.Properties = new Dictionary<string, Schema>();
            foreach (var (name, node) in props)
            {
                if (node is JsonObject propObj)
                    schema.Properties[name] = ToSchema(propObj);
            }
        }
        if (obj["required"] is JsonArray reqArr)
            schema.Required = reqArr.Select(x => x?.GetValue<string>() ?? "").Where(s => s.Length > 0).ToList();
        if (obj["enum"] is JsonArray enumArr)
            schema.Enum = enumArr.Select(x => x?.GetValue<string>() ?? "").Where(s => s.Length > 0).ToList();
        if (obj["description"]?.GetValue<string>() is { } desc)
            schema.Description = desc;
        return schema;
    }

    private GenerativeModel DefaultModelFactory(string modelId) =>
        new GoogleAi(apiKey).CreateGenerativeModel(modelId);
}
