namespace AgentSmith.Contracts.Models.Triggers;

/// <summary>
/// One (project, pipeline) match produced by IEnvelopeProjectResolver. Kind is the
/// trigger-block name that matched ("github"/"gitlab"/"azuredevops"/"jira") — useful
/// for diagnostics; the caller doesn't need it for spawn.
/// </summary>
public readonly record struct ProjectMatch(string ProjectName, string PipelineName, string Kind);
