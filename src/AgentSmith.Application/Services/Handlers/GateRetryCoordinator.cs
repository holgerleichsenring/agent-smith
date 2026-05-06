using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Runs a gate LLM call. If the first response fails to parse, retries
/// exactly once with a corrective prompt that shows the failed response
/// and the parse error. A second failure fails the pipeline.
/// </summary>
public sealed class GateRetryCoordinator(
    IGateOutputHandler gateOutputHandler,
    IChatClientFactory chatClientFactory,
    ILogger<GateRetryCoordinator> logger) : IGateRetryCoordinator
{
    private const int FailedResponseQuoteLimit = 2000;

    public async Task<GateCallOutcome> ExecuteAsync(
        RoleSkillDefinition role,
        SkillOrchestration orchestration,
        string systemPrompt,
        string userPromptPrefix,
        string userPromptSuffix,
        PipelineContext pipeline,
        CancellationToken cancellationToken)
    {
        var firstText = await CallAsync(systemPrompt, userPromptPrefix, userPromptSuffix, pipeline, cancellationToken);
        var firstResult = gateOutputHandler.Handle(role, orchestration, firstText, pipeline);
        if (firstResult.IsSuccess)
            return new GateCallOutcome(firstResult, firstText);

        logger.LogWarning(
            "Gate {Name} failed on first attempt: {Message}. Retrying once with corrective prompt.",
            role.DisplayName, firstResult.Message);

        var correctiveSuffix = BuildCorrectiveSuffix(userPromptSuffix, firstText, firstResult.Message);
        var retryText = await CallAsync(systemPrompt, userPromptPrefix, correctiveSuffix, pipeline, cancellationToken);
        var retryResult = gateOutputHandler.Handle(role, orchestration, retryText, pipeline);

        if (retryResult.IsSuccess)
        {
            logger.LogInformation("Gate {Name} succeeded on retry", role.DisplayName);
            return new GateCallOutcome(retryResult, retryText);
        }

        logger.LogError(
            "Gate {Name} failed after retry: {Message}",
            role.DisplayName, retryResult.Message);
        return new GateCallOutcome(
            CommandResult.Fail($"Gate {role.DisplayName} failed after one retry: {retryResult.Message}"),
            retryText);
    }

    private async Task<string> CallAsync(
        string systemPrompt, string userPromptPrefix, string userPromptSuffix,
        PipelineContext pipeline, CancellationToken cancellationToken)
    {
        var agent = pipeline.Get<AgentConfig>(ContextKeys.AgentConfig);
        var chat = chatClientFactory.Create(agent, TaskType.Primary);
        var maxTokens = chatClientFactory.GetMaxOutputTokens(agent, TaskType.Primary);
        var combinedUser = string.IsNullOrEmpty(userPromptSuffix)
            ? userPromptPrefix
            : $"{userPromptPrefix}\n\n{userPromptSuffix}";
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, systemPrompt),
            new(ChatRole.User, combinedUser),
        };
        var response = await chat.GetResponseAsync(messages,
            new ChatOptions { MaxOutputTokens = maxTokens }, cancellationToken);
        PipelineCostTracker.GetOrCreate(pipeline).Track(response);
        return response.Text ?? string.Empty;
    }

    private static string BuildCorrectiveSuffix(
        string originalUserSuffix, string failedResponse, string parseError)
    {
        var quoted = failedResponse.Length <= FailedResponseQuoteLimit
            ? failedResponse
            : failedResponse[..FailedResponseQuoteLimit] + "...[truncated]";

        return $$"""
            {{originalUserSuffix}}

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
