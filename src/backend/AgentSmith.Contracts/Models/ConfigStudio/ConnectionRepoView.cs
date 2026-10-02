namespace AgentSmith.Contracts.Models.ConfigStudio;

/// <summary>One discovered repo: name + the provider-reported default branch.</summary>
public sealed record ConnectionRepoView(string Name, string? DefaultBranch);
