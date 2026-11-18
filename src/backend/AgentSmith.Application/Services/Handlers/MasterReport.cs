using AgentSmith.Application.Services.Tools;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-11-18-98fa: the coding master's standing verdict — what finish_spec recorded, with the master's report
/// beside it. The report is the master's last message with text up to its last finish_spec call, read over the whole
/// conversation: a master that ended a drive on its report and called finish_spec in the next still has it. Only when
/// no text precedes the call is the text after it the report. Nothing in it is parsed.
/// </summary>
internal static class MasterReport
{
    internal static MasterVerification? Standing(FinishSpecToolHost? finish, IReadOnlyList<ChatMessage> thread) =>
        finish?.Verification is { } recorded ? recorded with { Report = Of(thread) } : null;

    internal static string? Of(IReadOnlyList<ChatMessage> thread)
    {
        var call = LastIndex(thread, m => m.Contents.OfType<FunctionCallContent>().Any(c => c.Name == FinishSpecToolHost.Name));
        if (call < 0) return null;
        for (var i = call; i >= 0; i--)
            if (Text(thread[i]) is { } before) return before;
        for (var i = thread.Count - 1; i > call; i--)
            if (Text(thread[i]) is { } after) return after;
        return null;
    }

    private static string? Text(ChatMessage message) =>
        message.Role == ChatRole.Assistant && !string.IsNullOrWhiteSpace(message.Text) ? message.Text.Trim() : null;

    private static int LastIndex(IReadOnlyList<ChatMessage> thread, Func<ChatMessage, bool> match)
    {
        for (var i = thread.Count - 1; i >= 0; i--)
            if (match(thread[i])) return i;
        return -1;
    }
}
