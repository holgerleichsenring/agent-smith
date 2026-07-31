using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.WorkSpecs;
using AgentSmith.Domain.Entities;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.WorkSpecs;

/// <summary>
/// p0390: the one LLM call of DeriveSpecification. Mirrors ExpectationDrafter's
/// chat plumbing (factory + call scope + cost tracking); a spec that breaks the
/// caps or dangles a sample anchor is rejected BACK to the model with the
/// validation message — bounded retries, then the step reports the failure
/// instead of shipping a half contract.
/// </summary>
public sealed class WorkSpecDeriver(
    IChatClientFactory chatClientFactory,
    IPromptCatalog prompts,
    IWorkSpecSerializer serializer,
    WorkSpecValidator validator,
    IRunContextAccessor runContext,
    ILogger<WorkSpecDeriver> logger) : IWorkSpecDeriver
{
    private const int MaxAttempts = 3;

    public async Task<(WorkSpecDraft? Draft, string? Error)> DeriveAsync(
        Ticket ticket, WorkSpecArtifact? previous, string cause,
        AgentConfig agentConfig, PipelineContext pipeline, CancellationToken cancellationToken)
    {
        var key = previous?.Spec.Key ?? WorkSpecKeyFactory.For(ticket, pipeline).Value;
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, RenderSystemPrompt(pipeline)),
            new(ChatRole.User,
                WorkSpecPromptComposer.Compose(ticket, previous, cause, pipeline, serializer)),
        };
        string? lastError = null;
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            var text = await CallModelAsync(agentConfig, pipeline, messages, cancellationToken);
            var (draft, error) = ParseAndValidate(text, key);
            if (draft is not null) return (draft, null);
            lastError = error;
            logger.LogWarning("Work-spec derivation attempt {Attempt}/{Max} rejected: {Error}",
                attempt, MaxAttempts, error);
            messages.Add(new ChatMessage(ChatRole.Assistant, text));
            messages.Add(new ChatMessage(ChatRole.User,
                $"Your spec was rejected:\n{error}\nRespond again with ONLY the corrected JSON object."));
        }
        return (null, lastError);
    }

    private string RenderSystemPrompt(PipelineContext pipeline) => prompts.Render(
        "work-spec-derivation-system", new Dictionary<string, string>
        {
            ["MaxRequirements"] = WorkSpec.MaxRequirements.ToString(),
            ["MaxConstraints"] = WorkSpec.MaxConstraints.ToString(),
            ["MaxAssumptions"] = WorkSpec.MaxAssumptions.ToString(),
            ["DoneInstruction"] = WorkSpecDoneSection.Instruction(pipeline),
        });

    private async Task<string> CallModelAsync(
        AgentConfig agentConfig, PipelineContext pipeline,
        List<ChatMessage> messages, CancellationToken cancellationToken)
    {
        var chat = chatClientFactory.Create(agentConfig, TaskType.Planning);
        var maxTokens = chatClientFactory.GetMaxOutputTokens(agentConfig, TaskType.Planning);
        using var _scope = runContext.BeginCallScope(
            "work-spec-derivation", SkillExecutionPhase.Plan.ToString());
        var response = await chat.GetResponseAsync(
            messages, new ChatOptions { MaxOutputTokens = maxTokens }, cancellationToken);
        PipelineCostTracker.GetOrCreate(pipeline).Track(response);
        return response.Text ?? string.Empty;
    }

    private (WorkSpecDraft? Draft, string? Error) ParseAndValidate(string text, string key)
    {
        var draft = WorkSpecDraftParser.TryParse(text, key);
        if (draft is null)
            return (null, "the reply contained no parseable JSON object with a 'goal' field");
        var errors = validator.Validate(draft.Artifact.Spec, draft.Artifact.SamplesMarkdown);
        return errors.Count == 0 ? (draft, null) : (null, string.Join("\n", errors));
    }
}
