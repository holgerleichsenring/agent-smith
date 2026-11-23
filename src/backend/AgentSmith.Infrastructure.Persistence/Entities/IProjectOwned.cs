namespace AgentSmith.Infrastructure.Persistence.Entities;

/// <summary>
/// 2026-11-23-47b6: a row that belongs to one project — what <see cref="Contracts.ProjectScopeQueries"/> filters
/// with one rule, so a run, a pull-request record and a filed series hide the same projects.
/// </summary>
public interface IProjectOwned
{
    string Project { get; }
}
