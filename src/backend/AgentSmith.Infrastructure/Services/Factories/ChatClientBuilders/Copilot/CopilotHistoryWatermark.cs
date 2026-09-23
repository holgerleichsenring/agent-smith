using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.AI;

namespace AgentSmith.Infrastructure.Services.Factories.ChatClientBuilders.Copilot;

/// <summary>
/// 2026-09-07-d5f2: remembers what a session has already been told, as a HASH of the message
/// prefix rather than a count. A count only notices messages arriving or leaving; an edit that
/// leaves the length alone — a compaction replacing a message, the sensitive-tool scrub rewriting
/// a tool result, a changed system prompt — would slip past it and leave the session silently
/// answering from a history the caller has since revised.
/// </summary>
internal static class CopilotHistoryWatermark
{
    internal static bool Extends(IReadOnlyList<string> sent, IReadOnlyList<string> incoming)
{
    if (incoming.Count < sent.Count) return false;
    for (var i = 0; i < sent.Count; i++)
        if (!string.Equals(sent[i], incoming[i], StringComparison.Ordinal))
            return false;
    return true;
}

internal static string Digest(ChatMessage message)
{
    var payload = $"{message.Role.Value}{message.Text}";
    return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
}
}
