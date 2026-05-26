namespace AgentSmith.Contracts.Models;

/// <summary>
/// A request to auto-fix security findings in a specific file and category.
/// Written as YAML to .agentsmith/security/fixes/ for pickup by a follow-up command.
/// </summary>
public sealed record SecurityFixRequest(
    string FilePath,
    string Category,
    string SuggestedBranch,
    IReadOnlyList<SecurityFixItem> Items);

/// <summary>
/// A single fixable finding within a SecurityFixRequest.
/// </summary>
public sealed record SecurityFixItem(
    string Severity,
    string Title,
    string Description,
    string? CweId,
    int Line);
