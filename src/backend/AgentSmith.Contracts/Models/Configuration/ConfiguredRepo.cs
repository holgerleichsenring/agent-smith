namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>A configured repository together with the name of the project that declares it.</summary>
public sealed record ConfiguredRepo(string ProjectName, ResolvedProject Project, RepoConnection Repo);
