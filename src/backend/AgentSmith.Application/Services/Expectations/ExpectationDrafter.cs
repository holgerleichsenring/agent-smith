using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Prompts;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Expectations;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Expectations;

/// <summary>
/// p0328: the one LLM call of the NegotiateExpectation step. Mirrors the
/// RepoScopeClassifier chat plumbing (factory + call scope + cost tracking);
/// a schema-overflowing draft is rejected BACK to the model with the
/// validation message — bounded retries, then the step fails loudly.
/// </summary>
public sealed class ExpectationDrafter(
    IChatClientFactory chatClientFactory,
    IPromptCatalog prompts,
    ExpectationDraftValidator validator,
    IRunContextAccessor runContext,
    ILogger<ExpectationDrafter> logger) : IExpectationDrafter
{
    private const int MaxAttempts = 3;

    public async Task<(ExpectationDraft? Draft, string? Error)> DraftAsync(
        Ticket ticket, AgentConfig agentConfig, PipelineContext pipeline,
        CancellationToken cancellationToken)
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, RenderSystemPrompt()),
            new(ChatRole.User, ExpectationPromptComposer.ComposeUserPrompt(ticket, pipeline)),
        };
        string? lastError = null;
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            var text = await CallModelAsync(agentConfig, pipeline, messages, cancellationToken);
            var (draft, error) = ParseAndValidate(text);
            if (draft is not null) return (draft, null);
            lastError = error;
            logger.LogWarning("Expectation draft attempt {Attempt}/{Max} rejected: {Error}",
                attempt, MaxAttempts, error);
            messages.Add(new ChatMessage(ChatRole.Assistant, text));
            messages.Add(new ChatMessage(ChatRole.User,
                $"Your draft was rejected:\n{error}\nRespond again with ONLY the corrected JSON object."));
        }
        return (null, lastError);
    }

    private string RenderSystemPrompt() => prompts.Render(
        "expectation-drafting-system", new Dictionary<string, string>
        {
            ["MaxExpected"] = ExpectationDraft.MaxExpected.ToString(),
            ["MaxConstraints"] = ExpectationDraft.MaxConstraints.ToString(),
        });

    private async Task<string> CallModelAsync(
        AgentConfig agentConfig, PipelineContext pipeline,
        List<ChatMessage> messages, CancellationToken cancellationToken)
    {
        var chat = chatClientFactory.Create(agentConfig, TaskType.Planning);
        var maxTokens = chatClientFactory.GetMaxOutputTokens(agentConfig, TaskType.Planning);
        using var _scope = runContext.BeginCallScope(
            "expectation-draft", SkillExecutionPhase.Plan.ToString());
        var response = await chat.GetResponseAsync(
            messages, new ChatOptions { MaxOutputTokens = maxTokens }, cancellationToken);
        PipelineCostTracker.GetOrCreate(pipeline).Track(response);
        return response.Text ?? string.Empty;
    }

    private (ExpectationDraft? Draft, string? Error) ParseAndValidate(string text)
    {
        var draft = ExpectationDraftParser.TryParse(text);
        if (draft is null)
            return (null, "the reply contained no parseable JSON object with an 'expected' array");
        var errors = validator.Validate(draft);
        return errors.Count == 0 ? (draft, null) : (null, string.Join("\n", errors));
    }
}
