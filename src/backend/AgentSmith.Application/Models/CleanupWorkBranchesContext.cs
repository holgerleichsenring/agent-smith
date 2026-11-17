using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Models;

/// <summary>2026-11-17-713ec: the repositories whose finished agent-smith/ branches the run removes.</summary>
public sealed record CleanupWorkBranchesContext(
    IReadOnlyList<RepoConnection> Repos, PipelineContext Pipeline) : ICommandContext;
