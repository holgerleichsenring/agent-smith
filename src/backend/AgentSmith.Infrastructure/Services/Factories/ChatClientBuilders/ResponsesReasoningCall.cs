#pragma warning disable OPENAI001 // the Responses types are marked experimental in OpenAI 2.8.0
using System.ClientModel;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Exceptions;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Responses;

namespace AgentSmith.Infrastructure.Services.Factories.ChatClientBuilders;

/// <summary>
/// 2026-11-25-9992k: how every call on the Responses API is made. Stateless: <c>store: false</c>, so nothing is kept
/// server-side and any replica can make the next call, with the reasoning items asked back encrypted
/// (<c>include: reasoning.encrypted_content</c>). The adapter keeps each item as the raw representation of its
/// <see cref="TextReasoningContent"/>, and the tool loop sends it back, id and encrypted content included, before the
/// tool output. The effort is sent as written, so a value the SDK has no name for (none, xhigh, max) still reaches the
/// provider. A caller's own raw options win, as on the Claude path. Azure serves the API at {resource}/openai/v1/ with
/// the deployment as the model and no api-version; the key goes as a bearer token, as Azure's own v1 samples send it.
/// </summary>
public static class ResponsesReasoningCall
{
    /// <param name="options">The builder's pipeline options (retries, quota, test transport); the endpoint is set here.</param>
    public static IChatClient Create(
        ApiKeyCredential credential, OpenAIClientOptions options, bool isAzure, string? endpoint, string model, string? effort)
    {
        if (isAzure)
            options.Endpoint = AzureV1(endpoint ?? throw new InvalidOperationException("Azure OpenAI requires AgentConfig.Endpoint."));
        else if (endpoint is not null)
            options.Endpoint = new Uri(endpoint);
        return new ChatClientBuilder(new OpenAIClient(credential, options).GetResponsesClient(model).AsIChatClient())
            .ConfigureOptions(call => call.RawRepresentationFactory ??= _ => Options(effort))
            .Build();
    }

    /// <summary>On Chat Completions a reasoning model calls tools only without an effort (gpt-5.6 fails at any other), so
    /// an effort there is refused by its keys rather than sent.</summary>
    public static void RefuseEffortOnChat(ModelAssignment assignment)
    {
        if (string.IsNullOrWhiteSpace(assignment.ReasoningEffort)) return;
        throw new ConfigurationException(
            $"reasoning_effort '{assignment.ReasoningEffort}' is set on model '{assignment.Model}' whose api is "
            + $"{ModelApi.ChatCompletions}: set api: {ModelApi.Responses} beside it, or remove reasoning_effort. On Chat "
            + "Completions a reasoning model calls tools only without an effort.");
    }

    /// <summary>An Azure resource endpoint as its v1 base: https://x.openai.azure.com/ and one that already ends in
    /// /openai or /openai/v1 all become https://x.openai.azure.com/openai/v1/.</summary>
    public static Uri AzureV1(string endpoint)
    {
        var root = endpoint.Trim().TrimEnd('/');
        if (root.EndsWith("/openai/v1", StringComparison.OrdinalIgnoreCase)) return new Uri(root + "/");
        if (root.EndsWith("/openai", StringComparison.OrdinalIgnoreCase)) return new Uri(root + "/v1/");
        return new Uri(root + "/openai/v1/");
    }

    public static CreateResponseOptions Options(string? effort)
    {
        var options = new CreateResponseOptions { StoredOutputEnabled = false };
        options.IncludedProperties.Add(IncludedResponseProperty.ReasoningEncryptedContent);
        if (!string.IsNullOrWhiteSpace(effort))
            options.ReasoningOptions = new ResponseReasoningOptions
            {
                ReasoningEffortLevel = new ResponseReasoningEffortLevel(effort.Trim()),
            };
        return options;
    }
}
