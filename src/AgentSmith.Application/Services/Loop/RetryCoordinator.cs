using System.Text.Json;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Loop;

/// <summary>
/// Wraps a single chat invocation with one failed_parse / failed_validation re-try.
/// On parse failure the constant <see cref="ParseRetryHint"/> is appended to the
/// message thread; on validation failure the validator's concrete error is appended
/// with the constant <see cref="ValidationRetryHintPrefix"/>. Single source of truth
/// for the retry-hint contract.
/// </summary>
public sealed class RetryCoordinator
{
    public const string ParseRetryHint = "last output was not valid JSON";
    public const string ValidationRetryHintPrefix = "output failed validation: ";

    public async Task<RetryOutcome> InvokeAsync(
        IChatClient chat,
        IList<ChatMessage> messages,
        ChatOptions options,
        ISkillOutputValidator validator,
        CancellationToken ct)
    {
        var first = await chat.GetResponseAsync(messages, options, ct);
        var firstText = first.Text ?? string.Empty;

        var firstParse = TryParse(firstText);
        if (!firstParse.Ok)
            return await RetryAsync(chat, messages, options, validator, firstText, RetryReason.Parse, ct);

        var firstValidation = validator.Validate(firstText);
        if (!firstValidation.IsValid)
            return await RetryAsync(chat, messages, options, validator, firstText,
                RetryReason.Validation, ct, firstValidation.ErrorMessage);

        return RetryOutcome.Ok(firstText);
    }

    private static async Task<RetryOutcome> RetryAsync(
        IChatClient chat, IList<ChatMessage> messages, ChatOptions options,
        ISkillOutputValidator validator, string firstText, RetryReason reason,
        CancellationToken ct, string? validationError = null)
    {
        AppendRetryHint(messages, firstText, reason, validationError);

        var second = await chat.GetResponseAsync(messages, options, ct);
        var secondText = second.Text ?? string.Empty;

        return ClassifyRetryResult(secondText, validator);
    }

    private static void AppendRetryHint(
        IList<ChatMessage> messages, string firstText, RetryReason reason, string? validationError)
    {
        var hint = reason == RetryReason.Parse
            ? ParseRetryHint
            : ValidationRetryHintPrefix + (validationError ?? "(no detail)");

        messages.Add(new ChatMessage(ChatRole.Assistant, firstText));
        messages.Add(new ChatMessage(ChatRole.User, hint));
    }

    private static RetryOutcome ClassifyRetryResult(string secondText, ISkillOutputValidator validator)
    {
        var parse = TryParse(secondText);
        if (!parse.Ok)
            return RetryOutcome.ParseFailed(parse.Error ?? "json parse failed after retry", secondText);

        var validation = validator.Validate(secondText);
        if (!validation.IsValid)
            return RetryOutcome.ValidationFailed(
                validation.ErrorMessage ?? "validation failed after retry", secondText);

        return RetryOutcome.Ok(secondText);
    }

    private static (bool Ok, string? Error) TryParse(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return (false, "empty response");

        try
        {
            using var _ = JsonDocument.Parse(text);
            return (true, null);
        }
        catch (JsonException ex)
        {
            return (false, ex.Message);
        }
    }

    private enum RetryReason { Parse, Validation }
}
