using AgentSmith.Contracts.Commands;
using AgentSmith.Application.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Builders;

/// <summary>2026-11-17-713ec: the run's project repositories — the step has no checkout to read them from.</summary>
public sealed class CleanupWorkBranchesContextBuilder : IContextBuilder
{
    public ICommandContext Build(PipelineCommand command, ResolvedProject project, PipelineContext pipeline) =>
        new CleanupWorkBranchesContext(project.Repos, pipeline);
}
