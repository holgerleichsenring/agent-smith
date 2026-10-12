using System.ClientModel;
using System.Net;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Exceptions;
using Microsoft.Extensions.AI;

namespace AgentSmith.Infrastructure.Services.Providers.Agent;

/// <summary>
/// 2026-10-12-6bfae: turns a provider's "not found" answer to a model call into a message that
/// names the role, its catalog entry and the model id, and says where to fix it. The bare
/// provider text ("model: anthropic.claude-sonnet-5-5") was the whole run summary and said
/// nothing about a rejected id.
///
/// <para>Classified on the status code, not on text: the Anthropic SDK raises
/// HttpRequestException with StatusCode, the OpenAI and Azure SDKs ClientResultException with
/// Status. A 404 on a chat call means the provider has no such model id, deployment or path.</para>
/// </summary>
internal sealed class ModelIdRejectionChatClient(
    IChatClient inner, string role, string modelId, string explanation)
    : DelegatingChatClient(inner)
{
    private const string AzureOpenAi = "azure_openai";

    /// <summary>Wraps a role's client with the rejection named for that role.</summary>
    internal static IChatClient Wrap(
        IChatClient inner, AgentConfig agent, TaskType task, ModelAssignment assignment, string providerType)
    {
        var chain = new ModelRoleChain(agent);
        var role = ModelRoleSlots.For(task).Key;
        var model = string.IsNullOrEmpty(assignment.Model) ? chain.PrimaryModel : assignment.Model;
        var deployment = assignment.Deployment ?? agent.Deployment;
        return new ModelIdRejectionChatClient(
            inner, role, model, Explain(role, chain.EntryFor(task), model, deployment, providerType));
    }

    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await base.GetResponseAsync(messages, options, cancellationToken);
        }
        catch (Exception ex) when (IsNotFound(ex))
        {
            throw new ModelIdRejectedException(role, modelId, explanation, ex);
        }
    }

    /// <summary>True when the exception chain carries a provider's 404 status.</summary>
    internal static bool IsNotFound(Exception exception)
    {
        for (Exception? e = exception; e is not null; e = e.InnerException)
            if (e is HttpRequestException { StatusCode: HttpStatusCode.NotFound } or ClientResultException { Status: 404 })
                return true;
        return false;
    }

    /// <summary>
    /// The operator-facing explanation. It never quotes the provider's body: a JSON payload in a
    /// failed summary is rewritten by the run-summary humanizer into an unrelated sentence.
    /// </summary>
    internal static string Explain(string role, string? entry, string model, string? deployment, string providerType)
    {
        var azure = string.Equals(providerType, AzureOpenAi, StringComparison.OrdinalIgnoreCase);
        var subject = azure ? $"deployment '{deployment ?? model}' (model id '{model}')" : $"model id '{model}'";
        var runs = entry is null ? $"the '{role}' model role" : $"the '{role}' model role (catalog entry '{entry}')";
        return $"The {providerType} provider rejected the {subject} that {runs} runs: it answered 404 not found. "
            + "Check the agent's model catalog in Config Studio (Agents, Models tab): the id must be the provider's own "
            + "spelling — an id from another provider's catalog (a Bedrock, Vertex or router-prefixed spelling) is refused"
            + (azure ? ", and the deployment must exist on the Azure resource" : string.Empty)
            + ". A wrong endpoint path is answered the same way.";
    }
}
