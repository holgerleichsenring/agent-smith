using AgentSmith.Application.Services.Deployment;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Models;

/// <summary>2026-11-18-3863c: context for the DeployToEnvironment step — the project and what the server seeded; a
/// request missing its context or environment reaches the handler as null.</summary>
public sealed record DeployToEnvironmentContext(PipelineContext Pipeline, ResolvedProject Project, DeployRequest? Request)
    : ICommandContext;
