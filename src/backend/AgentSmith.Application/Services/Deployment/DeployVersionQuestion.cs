using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Services.Deployment;

/// <summary>2026-11-18-3863c: what deciding a deploy's version needs — the project, the target, the template's declaration,
/// the declared environment and the version the person typed, if any. 2026-11-18-3863h: the version a server promotion
/// carries from the settled source deploy.</summary>
public sealed record DeployVersionQuestion(
    ResolvedProject Project, DeploymentTarget Target, DeploymentDeclaration Declaration, DeclaredEnvironment Declared,
    string? Requested,
    string? Promoted = null);
