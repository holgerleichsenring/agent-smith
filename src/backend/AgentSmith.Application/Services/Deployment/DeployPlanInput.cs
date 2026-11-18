using AgentSmith.Application.Models;

namespace AgentSmith.Application.Services.Deployment;

/// <summary>2026-11-18-3863c: what planning a deploy starts from — the step's context, its target, the template's
/// declaration and the sha the declaration was read at.</summary>
public sealed record DeployPlanInput(
    DeployToEnvironmentContext Context, DeploymentTarget Target, DeploymentDeclaration Declaration, string? TemplateSha);
