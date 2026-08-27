using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.AI;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// p0385: the exploration's terminal turn. When an attempt burns its tool-call budget
/// (or answers with prose), the SAME conversation continues with tool use disabled and
/// one instruction demanding the JSON from the evidence already gathered — a shallow map
/// beats a blank restart that hits the same cap again. Extracted from ProjectAnalyzer
/// (2026-08-27-3eb1): sweeping a repository and closing a conversation are two reasons
/// to change.
/// </summary>
public interface IProjectMapFinalizer
{
    /// <summary>
    /// Continues <paramref name="messages"/> with the finalize turn. Returns the parsed
    /// map, or null plus the parse error that stopped it.
    /// </summary>
    Task<(ProjectMap? Map, string Error)> FinalizeAsync(
        IChatClient chat, ChatOptions options, List<ChatMessage> messages,
        ChatResponse exploration, int attempt, string? repoName,
        CancellationToken cancellationToken);
}
