using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Deployment;

/// <summary>
/// 2026-11-18-3863c: reads a deployment binding's declaration out of its template at the bound revision, through the
/// same read-only template scope a coding run opens — the checkout-free branch read cannot reach a tag on every host.
/// The scope is disposed when the read ends.
/// </summary>
public sealed class DeploymentTemplateRead(
    ProjectTemplateScopes scopes, ISandboxFileReaderFactory files, DeploymentDeclarationParser parser,
    ILogger<DeploymentTemplateRead> logger)
{
    /// <summary>The declaration and the sha it was read at, or null with the reason.</summary>
    public async Task<(DeploymentDeclaration? Declaration, string? Sha, string? Error)> ReadAsync(
        ResolvedProject project, ProjectTemplate binding, CancellationToken cancellationToken)
    {
        var path = $"{ProjectMetaPaths.Contexts}/{binding.TemplateContext}/{ProjectMetaPaths.DeploymentFile}";
        await using var scope = scopes.Open(project, TemplateScopeName.For(binding), binding);
        string? text;
        try
        {
            await scope.MaterializeAsync(cancellationToken);
            text = await files.Create(scope).TryReadAsync(ContainedPath.Absolute(path), cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "The deployment template {Repo} could not be read", binding.Repo.Name);
            return (null, null, $"the deployment template `{binding.Repo.Name}` at '{binding.Revision ?? "its default branch"}' could not be read: {ex.Message}");
        }
        var at = $"`{path}` of `{binding.Repo.Name}` at {scope.ResolvedSha ?? binding.Revision ?? "its default branch"}";
        if (text is null) return (null, scope.ResolvedSha, $"the template has no {at}");
        var (declaration, error) = parser.Parse(text);
        return declaration is null ? (null, scope.ResolvedSha, $"{at} is refused: {error}") : (declaration, scope.ResolvedSha, null);
    }
}
