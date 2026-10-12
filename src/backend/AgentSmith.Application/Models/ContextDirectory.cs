using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Models;

/// <summary>
/// 2026-10-12-6bfaa: where a pre-sandbox context read found <c>.agentsmith/contexts</c> — the
/// branch (null = the default branch), the entries listed there, and every candidate branch
/// passed over on the way, each with the reason.
/// </summary>
public sealed record ContextDirectory(
    BranchName? Branch, IReadOnlyList<string> Children, IReadOnlyList<string> PassedOver)
{
    /// <summary>The branch in words, for a provenance line and a log.</summary>
    public string Where => Branch is null ? "the default branch" : $"branch '{Branch.Value}'";

    /// <summary>What was searched before this branch, or empty when nothing was passed over.</summary>
    public string Searched => PassedOver.Count == 0 ? string.Empty : string.Join("; ", PassedOver);
}
