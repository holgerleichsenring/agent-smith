using System.Security.Cryptography;
using System.Text;
using AgentSmith.Contracts.Models.Compaction;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;

namespace AgentSmith.Infrastructure.Services.Providers.Agent.Compaction;

/// <summary>
/// LLM-based compactor for the OpenAI / Azure-OpenAI agentic loops. Summarizes the
/// pre-tail prefix into a single user message; keeps the original system prompt
/// + the last N complete tool-call rounds verbatim. Trigger is boolean OR:
/// iterations OR estimated tokens.
/// </summary>
public sealed class OpenAiContextCompactor(
    ChatClient summarizerClient,
    CompactionConfig config,
    IPromptCatalog prompts,
    ILogger<OpenAiContextCompactor> logger) : IOpenAiContextCompactor
{
    private const string PromptName = "openai-context-compactor-system";
    private const int CharsPerTokenEstimate = 4;
    private const int KeepRounds = 2;

    private string? _cachedPrompt;
    private string? _cachedPromptHash;

    public async Task<OpenAiCompactionResult> CompactIfNeededAsync(
        IReadOnlyList<ChatMessage> messages,
        int currentIterations,
        int estimatedAccumulatedTokens,
        CancellationToken cancellationToken)
    {
        if (!config.IsEnabled)
            return new OpenAiCompactionResult(messages, Event: null);

        if (!ShouldFire(currentIterations, estimatedAccumulatedTokens))
            return new OpenAiCompactionResult(messages, Event: null);

        var tailStart = ToolCallRoundIdentifier.FindTailStartIndex(messages, KeepRounds);
        var hasSystem = messages.Count > 0 && messages[0] is SystemChatMessage;
        var prefixSize = hasSystem ? tailStart - 1 : tailStart;
        var tailSize = messages.Count - tailStart;
        if (prefixSize < 2 || tailSize == 0)
        {
            // Either the prefix is too small to be worth summarizing (just system + initial user),
            // or no tail messages exist (nothing to preserve verbatim). No-op in both cases.
            logger.LogDebug(
                "Compaction trigger crossed but prefixSize={Prefix}, tailSize={Tail} — skipping",
                prefixSize, tailSize);
            return new OpenAiCompactionResult(messages, Event: null);
        }

        return await DoCompactAsync(messages, tailStart, estimatedAccumulatedTokens, cancellationToken);
    }

    private bool ShouldFire(int iterations, int estimatedTokens) =>
        (iterations >= config.ThresholdIterations) ||
        (estimatedTokens >= config.MaxContextTokens);

    private async Task<OpenAiCompactionResult> DoCompactAsync(
        IReadOnlyList<ChatMessage> messages, int tailStart,
        int estimatedTokens, CancellationToken cancellationToken)
    {
        var systemMessage = ExtractSystem(messages);
        var prefix = SlicePrefix(messages, systemMessage is null ? 0 : 1, tailStart);
        var tail = messages.Skip(tailStart).ToList();
        var promptHash = ResolvePromptHash();

        var summary = await TrySummarize(prefix, cancellationToken);
        if (summary is null)
        {
            return new OpenAiCompactionResult(
                messages,
                CompactionEvent.ForFailure(prefix.Count, estimatedTokens, promptHash, "summarizer call failed"));
        }

        var compacted = BuildCompactedList(systemMessage, summary.Value.SummaryText, tail);
        logger.LogInformation(
            "Compacted {Old} → {New} messages (est. {Estimate} tokens before; verified count from next response)",
            messages.Count, compacted.Count, estimatedTokens);

        return new OpenAiCompactionResult(
            compacted,
            new CompactionEvent(
                OldMessageCount: messages.Count,
                NewMessageCount: compacted.Count,
                PreCompactionEstimatedTokens: estimatedTokens,
                PostCompactionVerifiedTokens: null,
                SummarizationCallTokens: summary.Value.TokensUsed,
                PromptHash: promptHash,
                Failed: false,
                FailureReason: null));
    }

    private static SystemChatMessage? ExtractSystem(IReadOnlyList<ChatMessage> messages) =>
        messages.Count > 0 && messages[0] is SystemChatMessage sys ? sys : null;

    private static List<ChatMessage> SlicePrefix(IReadOnlyList<ChatMessage> messages, int from, int toExclusive)
    {
        var prefix = new List<ChatMessage>(toExclusive - from);
        for (var i = from; i < toExclusive; i++) prefix.Add(messages[i]);
        return prefix;
    }

    private async Task<(string SummaryText, int TokensUsed)?> TrySummarize(
        IReadOnlyList<ChatMessage> prefix, CancellationToken cancellationToken)
    {
        try
        {
            var promptText = ResolvePrompt();
            var serialized = SerializePrefix(prefix);

            var summaryMessages = new List<ChatMessage>
            {
                new SystemChatMessage(promptText),
                new UserChatMessage(serialized)
            };

            var completion = await summarizerClient.CompleteChatAsync(summaryMessages, new ChatCompletionOptions(), cancellationToken);
            var text = completion.Value.Content.FirstOrDefault()?.Text ?? string.Empty;
            var tokens = (completion.Value.Usage?.InputTokenCount ?? 0) + (completion.Value.Usage?.OutputTokenCount ?? 0);
            return (text, tokens);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "OpenAi context compactor: summarizer call failed — falling back to original messages");
            return null;
        }
    }

    private static List<ChatMessage> BuildCompactedList(
        SystemChatMessage? system, string summaryText, List<ChatMessage> tail)
    {
        var result = new List<ChatMessage>(2 + tail.Count);
        if (system is not null) result.Add(system);
        result.Add(new UserChatMessage($"[Context Summary from previous iterations]\n{summaryText}"));
        result.AddRange(tail);
        return result;
    }

    private static string SerializePrefix(IReadOnlyList<ChatMessage> prefix)
    {
        var parts = new List<string>(prefix.Count);
        foreach (var m in prefix) parts.Add(SerializeMessage(m));
        return string.Join("\n\n", parts);
    }

    private static string SerializeMessage(ChatMessage m) => m switch
    {
        UserChatMessage u => $"[User] {string.Join("", u.Content.Select(c => c.Text))}",
        AssistantChatMessage a => SerializeAssistant(a),
        ToolChatMessage t => $"[Tool Result {Trunc(t.ToolCallId)}] {string.Join("", t.Content.Select(c => c.Text))}",
        SystemChatMessage s => $"[System] {string.Join("", s.Content.Select(c => c.Text))}",
        _ => $"[{m.GetType().Name}]"
    };

    private static string SerializeAssistant(AssistantChatMessage a)
    {
        var text = string.Join("", a.Content.Select(c => c.Text));
        if (a.ToolCalls.Count == 0) return $"[Assistant] {text}";
        var calls = string.Join(", ", a.ToolCalls.Select(tc => $"{tc.FunctionName}({Trunc(tc.Id)})"));
        return $"[Assistant] {text} [tool_calls: {calls}]";
    }

    private static string Trunc(string s) => s.Length <= 8 ? s : s[..8];

    private string ResolvePrompt() => _cachedPrompt ??= prompts.Get(PromptName);

    private string ResolvePromptHash()
    {
        if (_cachedPromptHash is not null) return _cachedPromptHash;
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(ResolvePrompt()));
        _cachedPromptHash = Convert.ToHexString(bytes)[..8].ToLowerInvariant();
        return _cachedPromptHash;
    }

    /// <summary>Token-count estimate for trigger evaluation. ~4 chars/token; ±5% accuracy.</summary>
    public static int EstimateTokens(IReadOnlyList<ChatMessage> messages)
    {
        var chars = 0;
        foreach (var m in messages)
        {
            switch (m)
            {
                case UserChatMessage u: foreach (var c in u.Content) chars += c.Text?.Length ?? 0; break;
                case AssistantChatMessage a:
                    foreach (var c in a.Content) chars += c.Text?.Length ?? 0;
                    foreach (var tc in a.ToolCalls) chars += tc.FunctionArguments.ToString().Length;
                    break;
                case ToolChatMessage t: foreach (var c in t.Content) chars += c.Text?.Length ?? 0; break;
                case SystemChatMessage s: foreach (var c in s.Content) chars += c.Text?.Length ?? 0; break;
            }
        }
        return chars / CharsPerTokenEstimate;
    }
}
