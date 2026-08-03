using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Infrastructure.Core.Services.Configuration;

/// <summary>
/// p0391b: the one place a configuration-resolution error becomes a finding. Every error
/// the raw-to-typed pipeline used to aggregate into a single throw is now built here, so
/// each one carries the project and the field an operator has to edit — six mistakes are
/// six lines, each pointing at its own key.
/// </summary>
public static class ProjectFindings
{
    public static StartupFinding Blocking(string project, string field, string reason) =>
        new(StartupSubsystems.Configuration, StartupFindingSeverity.Blocking,
            reason, project, Field: field);
}
