using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Services.Deployment;

/// <summary>2026-11-18-3863c: what deciding a deploy's version needs — the project, the target, the template's declaration,
/// the declared environment and the version the person typed, if any.</summary>
public sealed record DeployVersionQuestion(
    ResolvedProject Project, DeploymentTarget Target, DeploymentDeclaration Declaration, DeclaredEnvironment Declared,
    string? Requested);
