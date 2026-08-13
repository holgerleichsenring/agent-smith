using AgentSmith.Contracts.Models.Workers;
using Microsoft.Extensions.AI;

namespace AgentSmith.Infrastructure.Services.Workers;

/// <summary>
/// p0416: renders the sampling options into the worker protocol AND names the ones it
/// cannot render. Everything a caller sets that has no field in
/// <see cref="WorkerRequestOptions"/> is listed in <c>not_rendered</c>: the worker is
/// told what it is not being shown, so "the worker sees what the provider sees" is a
/// checkable claim rather than an assurance.
/// </summary>
public sealed class WorkerOptionsMapper
{
    public WorkerRequestOptions Map(ChatOptions? options) => options is null
        ? new WorkerRequestOptions()
        : new WorkerRequestOptions(
            options.MaxOutputTokens,
            options.Temperature,
            options.TopP,
            options.StopSequences is { Count: > 0 } stops ? [.. stops] : null,
            DescribeToolMode(options.ToolMode),
            options.AllowMultipleToolCalls,
            options.ResponseFormat is null ? null : DescribeResponseFormat(options.ResponseFormat),
            NotRendered(options));

    private static string? DescribeToolMode(ChatToolMode? mode) => mode switch
    {
        null => null,
        NoneChatToolMode => "none",
        AutoChatToolMode => "auto",
        RequiredChatToolMode required => required.RequiredFunctionName is { } name
            ? $"required:{name}" : "required",
        _ => mode.GetType().Name,
    };

    private static string DescribeResponseFormat(ChatResponseFormat format) => format switch
    {
        ChatResponseFormatJson json => json.Schema?.ToString() ?? "json",
        _ => "text",
    };

    // The properties a provider would honour and this envelope has no place for. Only
    // reported when actually SET — an unused knob is not a fidelity gap.
    private static IReadOnlyList<string>? NotRendered(ChatOptions options)
    {
        List<string> missing = [];
        if (options.TopK is not null) missing.Add(nameof(options.TopK));
        if (options.FrequencyPenalty is not null) missing.Add(nameof(options.FrequencyPenalty));
        if (options.PresencePenalty is not null) missing.Add(nameof(options.PresencePenalty));
        if (options.Seed is not null) missing.Add(nameof(options.Seed));
        if (options.Instructions is not null) missing.Add(nameof(options.Instructions));
        if (options.ConversationId is not null) missing.Add(nameof(options.ConversationId));
        if (options.Reasoning is not null) missing.Add(nameof(options.Reasoning));
        if (options.RawRepresentationFactory is not null) missing.Add(nameof(options.RawRepresentationFactory));
        if (options.AdditionalProperties is { Count: > 0 }) missing.Add(nameof(options.AdditionalProperties));
        return missing.Count == 0 ? null : missing;
    }
}
