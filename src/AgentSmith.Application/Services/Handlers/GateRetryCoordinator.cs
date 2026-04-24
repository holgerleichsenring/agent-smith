using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Runs a gate LLM call. If the first response fails to parse, retries
/// exactly once with a corrective prompt that shows the failed response
/// and the parse error. A second failure fails the pipeline.
/// </summary>
public sealed class GateRetryCoordinator(
    IGateOutputHandler gateOutputHandler,
    ILogger<GateRetryCoordinator> logger) : IGateRetryCoordinator
{
    private const int FailedResponseQuoteLimit = 2000;

    public async Task<GateCallOutcome> ExecuteAsync(
        RoleSkillDefinition role,
        SkillOrchestration orchestration,
        string systemPrompt,
        string userPrompt,
        ILlmClient llmClient,
        PipelineContext pipeline,
        CancellationToken cancellationToken)
    {
        var first = await CallAsync(llmClient, systemPrompt, userPrompt, pipeline, cancellationToken);
        var firstResult = gateOutputHandler.Handle(role, orchestration, first.Text, pipeline);
        if (firstResult.IsSuccess)
            return new GateCallOutcome(firstResult, first.Text);

        logger.LogWarning(
            "Gate {Name} failed on first attempt: {Message}. Retrying once with corrective prompt.",
            role.DisplayName, firstResult.Message);

        var correctivePrompt = BuildCorrectivePrompt(userPrompt, first.Text, firstResult.Message);
        var retry = await CallAsync(llmClient, systemPrompt, correctivePrompt, pipeline, cancellationToken);
        var retryResult = gateOutputHandler.Handle(role, orchestration, retry.Text, pipeline);

        if (retryResult.IsSuccess)
        {
            logger.LogInformation("Gate {Name} succeeded on retry", role.DisplayName);
            return new GateCallOutcome(retryResult, retry.Text);
        }

        logger.LogError(
            "Gate {Name} failed after retry: {Message}",
            role.DisplayName, retryResult.Message);
        return new GateCallOutcome(
            CommandResult.Fail($"Gate {role.DisplayName} failed after one retry: {retryResult.Message}"),
            retry.Text);
    }

    private static async Task<LlmResponse> CallAsync(
        ILlmClient llmClient, string systemPrompt, string userPrompt,
        PipelineContext pipeline, CancellationToken cancellationToken)
    {
        var response = await llmClient.CompleteAsync(
            systemPrompt, userPrompt, TaskType.Planning, cancellationToken);
        PipelineCostTracker.GetOrCreate(pipeline).Track(response);
        return response;
    }

    private static string BuildCorrectivePrompt(
        string originalUserPrompt, string failedResponse, string parseError)
    {
        var quoted = failedResponse.Length <= FailedResponseQuoteLimit
            ? failedResponse
            : failedResponse[..FailedResponseQuoteLimit] + "...[truncated]";

        return $$"""
            {{originalUserPrompt}}

            ## IMPORTANT: Your previous response could not be parsed.

            Previous response:
            ```
            {{quoted}}
            ```

            Parse error: {{parseError}}

            Respond again, strictly following the output format specified above.
            Return ONLY the required JSON — no markdown code fences, no prose before or after the JSON.
            """;
    }
}
