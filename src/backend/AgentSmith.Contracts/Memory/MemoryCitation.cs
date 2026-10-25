namespace AgentSmith.Contracts.Memory;

/// <summary>2026-10-25-9324b: one memory a master named — the run's repository, its ledger key, and the slug.</summary>
public sealed record MemoryCitation(string Repository, string RepoKey, string Slug);
