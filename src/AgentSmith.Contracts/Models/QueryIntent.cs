namespace AgentSmith.Contracts.Models;

/// <summary>
/// Represents a user query against the project knowledge base.
/// </summary>
public sealed record QueryIntent(
    string ProjectName,
    string Question,
    string ChannelId);
