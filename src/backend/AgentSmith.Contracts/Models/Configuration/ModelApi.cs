namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// 2026-11-25-9992k: the OpenAI wire a model is called on. Chat Completions is what every OpenAI and Azure role took
/// before and stays the default; the Responses API is the one a reasoning model of the GPT-6 family calls tools on and
/// returns reasoning items on, to carry across tool calls.
/// </summary>
public static class ModelApi
{
    public const string ChatCompletions = "chat_completions";
    public const string Responses = "responses";

    /// <summary>True for <see cref="Responses"/>, false for unset or <see cref="ChatCompletions"/>; any other value is
    /// refused by name.</summary>
    public static bool IsResponses(string? api) => api?.Trim().ToLowerInvariant() switch
    {
        null or "" or ChatCompletions => false,
        Responses => true,
        _ => throw new Domain.Exceptions.ConfigurationException(
            $"api '{api}' is not a model API (known: {ChatCompletions}, {Responses})."),
    };

    /// <summary>Both keys name an OpenAI wire: on any other provider they would be dropped unread, so they are refused
    /// by name instead.</summary>
    public static void RefuseOutsideOpenAi(string providerType, ModelAssignment assignment)
    {
        if (providerType.ToLowerInvariant() is "openai" or "azure_openai") return;
        if (string.IsNullOrWhiteSpace(assignment.Api) && string.IsNullOrWhiteSpace(assignment.ReasoningEffort)) return;
        throw new Domain.Exceptions.ConfigurationException(
            $"Model '{assignment.Model}' answers on provider '{providerType}', which has no api or reasoning_effort: "
            + "both apply to openai and azure_openai only. Remove them from this entry.");
    }
}
